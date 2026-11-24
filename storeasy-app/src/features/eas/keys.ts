// Query keys of the EAS screens.
export const easKeys = {
  all: ['eas'] as const,
  connection: (organizationId: number) => ['eas', 'connection', organizationId] as const,
  app: (organizationId: number, appId: number) => ['eas', 'app', organizationId, appId] as const,
  projects: (organizationId: number, appId: number) => ['eas', 'projects', organizationId, appId] as const,
  profiles: (organizationId: number, appId: number, gitRef: string) => ['eas', 'profiles', organizationId, appId, gitRef] as const,
}
