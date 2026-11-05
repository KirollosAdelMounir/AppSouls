import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const source=await fs.readFile(new URL('../TicketingCRM/wwwroot/transfer-format.js',import.meta.url),'utf8');
const {formatBytes}=await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
for(const [bytes,label] of [[0,'0 B'],[999,'999 B'],[1000,'1 KB'],[1e6,'1 MB'],[1e9,'1 GB'],[1e12,'1 TB'],[1e15,'1 PB'],[1e18,'1 EB']])assert.equal(formatBytes(bytes),label);
assert.equal(formatBytes(-1),'Unknown');assert.equal(formatBytes(NaN),'Unknown');
console.log('Transfer byte-unit formatting: 10 cases passed.');
