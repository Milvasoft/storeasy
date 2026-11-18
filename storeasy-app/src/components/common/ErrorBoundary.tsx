import { Component, type ErrorInfo, type ReactNode } from 'react'
import { TriangleAlert } from 'lucide-react'
import { getDictionary } from '@/lib/i18n/dictionaries'

interface ErrorBoundaryProps {
  children: ReactNode
  fullScreen?: boolean
}

interface ErrorBoundaryState {
  error: Error | null
}

const CHUNK_RELOAD_FLAG = 'storeasy.chunk-reload'

// A lazy page whose chunk disappeared with a new deploy fails to import; one reload fetches the new index.html.
export function isChunkLoadError(error: Error): boolean {
  return /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module/i.test(
    error.message,
  )
}

function reloadOnceForChunk(): boolean {
  try {
    if (sessionStorage.getItem(CHUNK_RELOAD_FLAG)) {
      return false
    }
    sessionStorage.setItem(CHUNK_RELOAD_FLAG, '1')
  } catch {
    return false
  }
  window.location.reload()
  return true
}

// Class component: React has no hook for render errors. The fallback reads the dictionary outside React, so it works
// even when a provider is what crashed. The kit's button classes are used directly for the same reason.
class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    if (isChunkLoadError(error) && reloadOnceForChunk()) {
      return
    }
    // The one hook for an error tracker, when the project gets one.
    console.error(error, info.componentStack)
  }

  render(): ReactNode {
    if (!this.state.error) {
      return this.props.children
    }
    const t = getDictionary()
    return (
      <div role="alert" className={this.props.fullScreen ? 'state min-h-full content-center bg-bg' : 'state'}>
        <div className="sic">
          <TriangleAlert aria-hidden />
        </div>
        <h1>{t.states.crashTitle}</h1>
        <p>{t.states.crashBody}</p>
        <button type="button" className="btn inv" onClick={() => window.location.reload()}>
          {t.states.reload}
        </button>
      </div>
    )
  }
}

export function RootErrorBoundary({ children }: { children: ReactNode }) {
  return <ErrorBoundary fullScreen>{children}</ErrorBoundary>
}

export function RouteErrorBoundary({ children }: { children: ReactNode }) {
  return <ErrorBoundary>{children}</ErrorBoundary>
}
