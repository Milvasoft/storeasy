import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { CircleAlert } from 'lucide-react'
import { safeReturnPath } from '@/components/common/ProtectedRoute'
import { AuthCard, SoloLayout } from '@/components/layout/AuthLayout'
import { SpinnerCenter } from '@/components/ui/primitives'
import { apiErrorMessage } from '@/lib/api/http'
import { useAuth } from '@/lib/auth/AuthContext'
import { readPending, writePending } from './GoogleSignIn'
import { useAccessText } from './i18n'

/** /auth/google/callback: Google's answer is checked and exchanged for a session. */
export function GoogleCallbackPage() {
  const t = useAccessText()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const { loginWithGoogle } = useAuth()
  const [failure, setFailure] = useState<string | null>(null)
  // The state works once on the server: React's development double effect must not send it twice.
  const sent = useRef(false)

  useEffect(() => {
    if (sent.current) {
      return
    }
    sent.current = true
    const code = params.get('code')
    const state = params.get('state')
    const pending = readPending()
    if (params.get('error')) {
      writePending(null)
      setFailure(t.google.denied)
      return
    }
    if (!code || !state) {
      setFailure(t.google.missing)
      return
    }
    if (pending && pending.state !== state) {
      writePending(null)
      setFailure(t.google.stateMismatch)
      return
    }
    writePending(null)
    loginWithGoogle(code, state, pending?.invitationToken)
      .then(() => navigate(safeReturnPath(pending?.returnTo) ?? '/', { replace: true }))
      .catch((error: unknown) => setFailure(apiErrorMessage(error, t.google.failedTitle)))
    // Runs once for the address Google sent the browser back to.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (failure) {
    return (
      <SoloLayout>
        <AuthCard icon={<CircleAlert aria-hidden />} tone="warn" title={t.google.failedTitle}>
          <p className="m-0 text-sm text-fg2">{failure}</p>
          <Link className="btn inv blk" to="/login">
            {t.google.backToLogin}
          </Link>
        </AuthCard>
      </SoloLayout>
    )
  }
  return (
    <SoloLayout>
      <SpinnerCenter label={t.google.callbackTitle} />
    </SoloLayout>
  )
}
