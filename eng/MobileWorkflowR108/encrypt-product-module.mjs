import fs from 'node:fs';
import { pathToFileURL } from 'node:url';
const input = JSON.parse(fs.readFileSync(0, 'utf8'));
const product = await import(pathToFileURL(input.modulePath).href);
const result = await product.encryptJson(...input.args);
process.stdout.write(JSON.stringify(result));
