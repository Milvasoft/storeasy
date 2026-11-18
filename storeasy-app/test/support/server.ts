import { setupServer } from 'msw/node'
import { defaultHandlers, shellHandlers } from './handlers'

export const server = setupServer(...defaultHandlers, ...shellHandlers)
