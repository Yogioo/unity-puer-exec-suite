import fs from 'fs';
import path from 'path';
import crypto from 'crypto';

const root = process.argv[2];
const skipDirs = new Set(['.git', 'Documentation~', 'node_modules']);
const skipFiles = new Set(['.gitignore']);

const IMPORTERS = {
  '.asmdef': 'AssemblyDefinitionImporter',
  '.cs': 'MonoImporter',
  '.json': 'PackageManifestImporter',
  '.shader': 'ShaderImporter',
};

const guidFor = (rel) => crypto.createHash('md5').update('com.yogioo.puer-exec-suite::' + rel).digest('hex');
const norm = (p) => p.split(path.sep).join('/');

const folderMeta = (rel) =>
  `fileFormatVersion: 2\nguid: ${guidFor(rel)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;

function fileMeta(rel) {
  const ext = path.extname(rel).toLowerCase();
  const guid = guidFor(rel);
  if (ext === '.cs') {
    return `fileFormatVersion: 2\nguid: ${guid}\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
  }
  const importer = IMPORTERS[ext] || 'DefaultImporter';
  return `fileFormatVersion: 2\nguid: ${guid}\n${importer}:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`;
}

let created = 0;

function walk(dir, rel) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const relChild = rel ? rel + '/' + entry.name : entry.name;
    const abs = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (skipDirs.has(entry.name)) continue;
      if (!fs.existsSync(abs + '.meta')) {
        fs.writeFileSync(abs + '.meta', folderMeta(norm(relChild)));
        created++;
      }
      walk(abs, relChild);
      continue;
    }
    if (entry.name.endsWith('.meta') || skipFiles.has(entry.name)) continue;
    if (!fs.existsSync(abs + '.meta')) {
      fs.writeFileSync(abs + '.meta', fileMeta(norm(relChild)));
      created++;
    }
  }
}

walk(root, '');
console.log('created metas:', created);
