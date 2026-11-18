// SPDX-License-Identifier: AGPL-3.0-only
import { run } from './main.js'

run().then((code) => {
  process.exitCode = code
})
