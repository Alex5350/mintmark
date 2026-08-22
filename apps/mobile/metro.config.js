const { getDefaultConfig } = require('expo/metro-config');
const path = require('node:path');

// pnpm workspace: @mintmark/ui-tokens is symlinked in from ../../packages,
// outside the project root, so Metro needs the workspace root on its watch
// list and both node_modules paths on the resolver.
const projectRoot = __dirname;
const workspaceRoot = path.resolve(projectRoot, '../..');

const config = getDefaultConfig(projectRoot);

config.watchFolders = [workspaceRoot];
config.resolver.nodeModulesPaths = [
  path.resolve(projectRoot, 'node_modules'),
  path.resolve(workspaceRoot, 'node_modules'),
];

module.exports = config;
