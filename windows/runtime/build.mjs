import { cp, lstat, mkdir, readFile, readdir, realpath, rm, writeFile } from 'node:fs/promises'
import { existsSync, globSync } from 'node:fs'
import { dirname, join, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'

const runtimeDir = dirname(fileURLToPath(import.meta.url))
const root = resolve(runtimeDir, '..', '..')
const distRoot = resolve(runtimeDir, 'dist')
const args = process.argv.slice(2)
const skipBuild = args.includes('--skip-build')
const outOption = option('--out') ?? join(distRoot, 'runtime')
const archiveOption = option('--archive') ?? join(distRoot, 'DeepSeek-Harness-Runtime-win-x64.zip')
const nodeOption = option('--node') ?? process.execPath
const output = resolve(outOption)
const archive = resolve(archiveOption)

assertInside(distRoot, output, 'runtime output')
assertInside(distRoot, archive, 'runtime archive')

run(process.execPath, [join(runtimeDir, 'generate-manifest.mjs'), '--check'])
run(process.execPath, ['--import', 'tsx/esm', join(root, 'scripts', 'verify-runtime-closure.ts'), '--manifest', join(runtimeDir, 'package.json')])
if (!skipBuild) run(pnpm(), ['run', 'build'], root)

await rm(output, { recursive: true, force: true })
await mkdir(dirname(output), { recursive: true })
run(pnpm(), [
  '--filter', '@deepseek-ai/dsh-windows-runtime', 'deploy', '--legacy', '--prod',
  '--config.node-linker=hoisted', '--config.auto-install-peers=false',
  '--config.link-workspace-packages=true', '--ignore-scripts', output,
], root)

await restoreMissingDirectPackages(output)
await materializeLinks(join(output, 'node_modules'))
await stageNode(output, nodeOption)

const repositoryManifest = JSON.parse(await readFile(join(root, 'package.json'), 'utf8'))
await writeFile(join(output, 'runtime-manifest.json'), `${JSON.stringify({
  schemaVersion: 1,
  product: 'DeepSeek Harness Windows Runtime',
  version: repositoryManifest.version,
  platform: 'win-x64',
  entry: 'node_modules/@deepseek-ai/dsh/lib/bin.js',
  node: 'tools/node/node.exe',
  minimumNode: '22.19.0',
  plugins: 'dsh plugin --profile <name> add <package-or-git-spec>',
}, null, 2)}\n`)

await rm(archive, { force: true })
await mkdir(dirname(archive), { recursive: true })
run('tar.exe', ['-a', '-c', '-f', archive, '-C', output, '.'])

const remaining = await findLink(output)
if (remaining !== undefined) throw new Error(`runtime still contains a link: ${remaining}`)
console.log(`windows runtime: staged ${output}`)
console.log(`windows runtime: archived ${archive}`)

function option(name) {
  const index = args.indexOf(name)
  if (index < 0) return undefined
  const value = args[index + 1]
  if (value === undefined || value.startsWith('--')) throw new Error(`${name} requires a value`)
  return value
}

function assertInside(parent, child, label) {
  const normalizedParent = parent.endsWith(sep) ? parent : `${parent}${sep}`
  if (child !== parent && !child.startsWith(normalizedParent)) {
    throw new Error(`${label} must stay under ${parent}: ${child}`)
  }
}

function pnpm() {
  return process.platform === 'win32' ? 'pnpm.cmd' : 'pnpm'
}

function run(command, commandArgs, cwd = root) {
  const shell = process.platform === 'win32' && /\.(?:cmd|bat)$/i.test(command)
  const result = spawnSync(command, commandArgs, { cwd, stdio: 'inherit', shell })
  if (result.error !== undefined) throw result.error
  if (result.status !== 0) throw new Error(`${command} exited with ${String(result.status)}`)
}

async function restoreMissingDirectPackages(staging) {
  const manifest = JSON.parse(await readFile(join(staging, 'package.json'), 'utf8'))
  const workspace = new Map()
  for (const relativePath of globSync([
    'apps/*/package.json',
    'packages/*/*/package.json',
    'vendor/*/package.json',
    'native/landlock-run/package.json',
    'native/landlock-run/packages/*/package.json',
  ], { cwd: root })) {
    const path = join(root, relativePath)
    const packageManifest = JSON.parse(await readFile(path, 'utf8'))
    if (packageManifest.name !== undefined) workspace.set(packageManifest.name, dirname(path))
  }

  const restored = []
  for (const dependency of Object.keys(manifest.dependencies ?? {}).sort()) {
    const destination = join(staging, 'node_modules', dependency)
    if (existsSync(destination)) continue
    const source = workspace.get(dependency)
    if (source === undefined) throw new Error(`missing deployed dependency has no workspace source: ${dependency}`)
    await mkdir(dirname(destination), { recursive: true })
    await copyPackageWithoutNodeModules(source, destination)
    restored.push(dependency)
  }
  if (restored.length > 0) console.log(`windows runtime: restored ${restored.join(', ')}`)
}

async function copyPackageWithoutNodeModules(source, destination) {
  const nestedNodeModules = join(source, 'node_modules')
  await cp(source, destination, {
    recursive: true,
    dereference: true,
    filter: path => path !== nestedNodeModules && !path.startsWith(`${nestedNodeModules}${sep}`),
  })
}

async function materializeLinks(nodeModules) {
  let link = await findLink(nodeModules)
  while (link !== undefined) {
    const segments = link.slice(nodeModules.length + 1).split(sep)
    const binIndex = segments.lastIndexOf('.bin')
    if (binIndex >= 0) {
      await rm(join(nodeModules, ...segments.slice(0, binIndex + 1)), { recursive: true, force: true })
    } else {
      const source = await realpath(link)
      await rm(link, { recursive: true, force: true })
      await copyPackageWithoutNodeModules(source, link)
    }
    link = await findLink(nodeModules)
  }
}

async function findLink(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    const metadata = await lstat(path)
    if (metadata.isSymbolicLink()) return path
    if (metadata.isDirectory()) {
      const nested = await findLink(path)
      if (nested !== undefined) return nested
    }
  }
  return undefined
}

async function stageNode(staging, nodeSource) {
  const source = resolve(nodeSource)
  if (!existsSync(source)) throw new Error(`node source does not exist: ${source}`)
  const destination = join(staging, 'tools', 'node')
  await mkdir(destination, { recursive: true })
  if ((await lstat(source)).isDirectory()) {
    await cp(source, destination, { recursive: true, dereference: true })
  } else {
    await cp(source, join(destination, 'node.exe'))
  }
  if (!existsSync(join(destination, 'node.exe'))) throw new Error('staged Node runtime has no node.exe')
}
