#!/usr/bin/env node
// Validates the storeasy store rules catalog. No dependencies.
//
//   node rules/validate.mjs
//
// 1. Every file listed in catalog.json (and catalog.json itself) is checked against
//    schema/catalog.schema.json by a small hand-rolled JSON Schema interpreter that
//    supports the keywords the schema uses.
// 2. Catalog checks: same catalogVersion everywhere, file store matches rule store and
//    id prefix, unique ids, fallbackTo/satisfiedBy/replacedBy references exist and do not
//    loop, dates are real calendar dates, deadline not before effectiveDate, every rule
//    has a source, no em or en dashes in text.
//
// Exit code 0 when valid, 1 otherwise.

import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = dirname(fileURLToPath(import.meta.url));
const errors = [];
const err = (file, path, msg) => errors.push(`${file}${path ? ' ' + path : ''}: ${msg}`);

function readJson(file) {
  const text = readFileSync(join(ROOT, file), 'utf8');
  if (text.charCodeAt(0) === 0xfeff) err(file, '', 'must not start with a BOM');
  if (text.includes('\r')) err(file, '', 'must use LF line endings');
  try {
    return JSON.parse(text);
  } catch (e) {
    err(file, '', `invalid JSON: ${e.message}`);
    return undefined;
  }
}

// ---------------------------------------------------------------- schema interpreter

const schema = readJson('schema/catalog.schema.json');

function resolveRef(ref) {
  if (!ref.startsWith('#/')) throw new Error(`unsupported $ref ${ref}`);
  return ref.slice(2).split('/').reduce((node, key) => node[key], schema);
}

function isRealDate(s) {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s);
  if (!m) return false;
  const d = new Date(Date.UTC(+m[1], +m[2] - 1, +m[3]));
  return d.getUTCFullYear() === +m[1] && d.getUTCMonth() === +m[2] - 1 && d.getUTCDate() === +m[3];
}

function typeOf(v) {
  if (v === null) return 'null';
  if (Array.isArray(v)) return 'array';
  if (typeof v === 'number') return Number.isInteger(v) ? 'integer' : 'number';
  return typeof v;
}

function typeMatches(v, t) {
  const actual = typeOf(v);
  if (t === 'number') return actual === 'number' || actual === 'integer';
  return actual === t;
}

const deepEqual = (a, b) => JSON.stringify(a) === JSON.stringify(b);

// Returns a list of error strings ("path: message").
function validate(node, value, path) {
  const out = [];
  const fail = (msg) => out.push(`${path || '/'}: ${msg}`);

  if (node === true) return out;
  if (node === false) return [`${path || '/'}: not allowed`];

  if (node.$ref) out.push(...validate(resolveRef(node.$ref), value, path));

  if (node.type !== undefined) {
    const types = Array.isArray(node.type) ? node.type : [node.type];
    if (!types.some((t) => typeMatches(value, t))) {
      fail(`expected ${types.join(' or ')}, got ${typeOf(value)}`);
      return out;
    }
  }
  if ('const' in node && !deepEqual(node.const, value)) fail(`must be ${JSON.stringify(node.const)}`);
  if (node.enum && !node.enum.some((e) => deepEqual(e, value))) fail(`must be one of ${node.enum.join(', ')}`);

  if (typeof value === 'string') {
    if (node.minLength !== undefined && [...value].length < node.minLength) fail(`shorter than ${node.minLength}`);
    if (node.maxLength !== undefined && [...value].length > node.maxLength) fail(`longer than ${node.maxLength}`);
    if (node.pattern && !new RegExp(node.pattern, 'u').test(value)) fail(`does not match ${node.pattern}`);
    if ((node.format === 'date' || node.format === 'date-time') && !isRealDate(value)) fail('not a real calendar date');
    if (node.format === 'uri') {
      try { new URL(value); } catch { fail('not a valid URI'); }
    }
  }

  if (typeof value === 'number') {
    if (node.minimum !== undefined && value < node.minimum) fail(`below minimum ${node.minimum}`);
    if (node.maximum !== undefined && value > node.maximum) fail(`above maximum ${node.maximum}`);
  }

  if (Array.isArray(value)) {
    if (node.minItems !== undefined && value.length < node.minItems) fail(`needs at least ${node.minItems} items`);
    if (node.maxItems !== undefined && value.length > node.maxItems) fail(`allows at most ${node.maxItems} items`);
    if (node.uniqueItems) {
      const seen = new Set();
      for (const item of value) {
        const k = JSON.stringify(item);
        if (seen.has(k)) fail(`duplicate item ${k}`);
        seen.add(k);
      }
    }
    if (node.items) value.forEach((item, i) => out.push(...validate(node.items, item, `${path}/${i}`)));
  }

  if (typeOf(value) === 'object') {
    const keys = Object.keys(value);
    if (node.minProperties !== undefined && keys.length < node.minProperties) fail(`needs at least ${node.minProperties} properties`);
    for (const r of node.required || []) if (!(r in value)) fail(`missing required property "${r}"`);
    const props = node.properties || {};
    for (const k of keys) {
      if (k in props) out.push(...validate(props[k], value[k], `${path}/${k}`));
      else if (node.additionalProperties === false) fail(`unknown property "${k}"`);
      else if (typeof node.additionalProperties === 'object') out.push(...validate(node.additionalProperties, value[k], `${path}/${k}`));
    }
  }

  if (node.allOf) for (const s of node.allOf) out.push(...validate(s, value, path));
  if (node.anyOf && !node.anyOf.some((s) => validate(s, value, path).length === 0)) fail('matches none of anyOf');
  if (node.oneOf) {
    const results = node.oneOf.map((s) => validate(s, value, path));
    const ok = results.filter((r) => r.length === 0).length;
    if (ok !== 1) {
      if (ok === 0) {
        // Report the branch with the fewest errors; it is usually the intended one.
        const best = results.reduce((a, b) => (b.length < a.length ? b : a));
        out.push(...best);
      } else fail(`matches ${ok} branches of oneOf, expected exactly 1`);
    }
  }
  if (node.if) {
    const matches = validate(node.if, value, path).length === 0;
    if (matches && node.then) out.push(...validate(node.then, value, path));
    if (!matches && node.else) out.push(...validate(node.else, value, path));
  }
  return out;
}

// ---------------------------------------------------------------- catalog checks

const DASHES = new RegExp('[' + String.fromCharCode(0x2013, 0x2014) + ']');
function walkStrings(value, path, fn) {
  if (typeof value === 'string') fn(value, path);
  else if (Array.isArray(value)) value.forEach((v, i) => walkStrings(v, `${path}/${i}`, fn));
  else if (value && typeof value === 'object') for (const [k, v] of Object.entries(value)) walkStrings(v, `${path}/${k}`, fn);
}

function main() {
  if (!schema) return;
  const manifest = readJson('catalog.json');
  if (!manifest) return;
  for (const e of validate(schema, manifest, '')) err('catalog.json', '', e);
  if (manifest.kind !== 'manifest') err('catalog.json', '', 'kind must be "manifest"');

  const listed = new Set((manifest.files || []).map((f) => f.path));
  const onDisk = readdirSync(ROOT).filter((f) => f.endsWith('.json') && f !== 'catalog.json');
  for (const f of onDisk) if (!listed.has(f)) err(f, '', 'rules file is not listed in catalog.json');

  const rules = new Map(); // id -> { file, rule }
  const stores = new Set();
  for (const entry of manifest.files || []) {
    const file = entry.path;
    if (stores.has(entry.store)) err('catalog.json', '', `store "${entry.store}" listed twice`);
    stores.add(entry.store);
    let doc;
    try { doc = readJson(file); } catch (e) { err(file, '', `cannot read: ${e.message}`); continue; }
    if (!doc) continue;
    for (const e of validate(schema, doc, '')) err(file, '', e);
    if (doc.kind !== 'store-rules') err(file, '', 'kind must be "store-rules"');
    if (doc.store !== entry.store) err(file, '', `store "${doc.store}" differs from catalog.json entry "${entry.store}"`);
    if (doc.catalogVersion !== manifest.catalogVersion) err(file, '', `catalogVersion ${doc.catalogVersion} differs from catalog.json ${manifest.catalogVersion}`);

    walkStrings(doc, '', (s, p) => { if (DASHES.test(s)) err(file, p, 'contains an em or en dash'); });

    (doc.rules || []).forEach((r, i) => {
      const p = `/rules/${i}`;
      if (!r || typeof r !== 'object') return;
      if (r.store !== doc.store) err(file, p, `rule store "${r.store}" differs from file store "${doc.store}"`);
      if (typeof r.id === 'string' && !r.id.startsWith(`${r.store}.${r.category}.`)) err(file, p, `id "${r.id}" must start with "${r.store}.${r.category}."`);
      if (rules.has(r.id)) err(file, p, `duplicate id "${r.id}" (also in ${rules.get(r.id).file})`);
      else rules.set(r.id, { file, rule: r, path: p });
      if (!Array.isArray(r.sources) || r.sources.length === 0) err(file, p, 'rule has no source');
      if (r.effectiveDate && r.deadline && r.deadline < r.effectiveDate) err(file, p, 'deadline is before effectiveDate');
      if (r.requirement?.level === 'required-from' && !r.requirement.from) err(file, p, 'required-from needs requirement.from');
      if (r.requirement?.from && r.requirement.level !== 'required-from') err(file, p, 'requirement.from is only for level required-from');
      if (r.verified === false && !r.notes) err(file, p, 'unverified rule needs notes explaining what is unverified');
      if (r.effectiveDate === null && r.requirement?.level === 'required-from' && !r.dateNote) err(file, p, 'approximate date needs dateNote');
    });
  }

  // References.
  for (const [id, { file, rule, path }] of rules) {
    const refs = [
      ...(rule.requirement?.fallbackTo || []).map((x) => ['fallbackTo', x]),
      ...(rule.requirement?.satisfiedBy || []).map((x) => ['satisfiedBy', x]),
      ...(rule.deprecated?.replacedBy ? [['replacedBy', rule.deprecated.replacedBy]] : []),
    ];
    for (const [kind, ref] of refs) {
      if (ref === id) err(file, path, `${kind} references itself`);
      else if (!rules.has(ref)) err(file, path, `${kind} references unknown rule "${ref}"`);
      else if (rules.get(ref).rule.store !== rule.store) err(file, path, `${kind} "${ref}" is in another store`);
    }
  }

  // Fallback chains must end (no cycles longer than the documented two-way pairs).
  // Apple documents mutual fallbacks (for example Face ID large <-> Dynamic Island large),
  // so a cycle is allowed only when every rule in it has satisfiedBy or is optional.
  for (const [id, { file, path }] of rules) {
    const seen = [id];
    let cur = rules.get(id).rule.requirement?.fallbackTo?.[0];
    while (cur && rules.has(cur)) {
      if (seen.includes(cur)) {
        const loop = seen.slice(seen.indexOf(cur));
        const blocking = loop.filter((x) => {
          const r = rules.get(x).rule;
          return r.requirement.level !== 'optional' && !r.requirement.satisfiedBy && x !== loop[0];
        });
        if (loop.length > 2 || blocking.length) err(file, path, `fallback cycle: ${[...loop, cur].join(' -> ')}`);
        break;
      }
      seen.push(cur);
      cur = rules.get(cur).rule.requirement?.fallbackTo?.[0];
    }
  }

  const counts = {};
  for (const { rule } of rules.values()) counts[rule.store] = (counts[rule.store] || 0) + 1;
  return { version: manifest.catalogVersion, counts, total: rules.size };
}

const summary = main();
if (errors.length) {
  console.error(`Catalog INVALID (${errors.length} error${errors.length === 1 ? '' : 's'}):`);
  for (const e of errors) console.error('  ' + e);
  process.exit(1);
}
const parts = Object.entries(summary.counts).map(([s, n]) => `${s} ${n}`).join(', ');
console.log(`Catalog ${summary.version} valid: ${summary.total} rules (${parts}).`);
