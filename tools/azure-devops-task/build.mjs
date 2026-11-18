// SPDX-License-Identifier: AGPL-3.0-only
// Bundles src/ with the storeasy library into the file the runner executes (committed). See ../scripts/bundle.mjs.
import { bundle } from '../scripts/bundle.mjs'

await bundle('azure-devops-task')
