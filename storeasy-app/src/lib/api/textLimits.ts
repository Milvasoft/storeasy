// Mirrors the API validators' limits, so an input never lets the user type what the API will refuse.
export const TEXT_LIMITS = {
  // Storeasy.Domain Organization.NameMaxLength
  organizationName: 100,
  // Storeasy.Domain StoreConnection.NameMaxLength
  connectionName: 100,
  // Storeasy.Domain User.NameMaxLength
  personName: 100,
  // AddStoreConnectionCommandValidator.SecretMaxLength (16 KB)
  connectionSecret: 16 * 1024,
  email: 256,
  password: 128,
} as const

// The password policy configured on the API (appsettings Identity.Password); there is no endpoint that returns it yet
// (design 5.8 plans system/public-settings), so the hints mirror the configuration.
export const PASSWORD_POLICY = {
  minLength: 8,
  requireUpper: true,
  requireLower: true,
  requireDigit: true,
  requireSymbol: true,
} as const
