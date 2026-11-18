// SPDX-License-Identifier: AGPL-3.0-only
// Fails when a committed bundle (action or task) is older than its sources: run npm run build and commit.
import { BUNDLES, isFresh } from './bundle.mjs'

let stale = false
for (const pkg of Object.keys(BUNDLES)) {
  if (!(await isFresh(pkg))) {
    console.error(`${pkg}: ${BUNDLES[pkg].outfile} is out of date; run "npm run build" and commit it.`)
    stale = true
  }
}
if (stale) {
  process.exit(1)
}
console.log('check-bundles: ok')
