DELETE FROM "StoreAuditLogs" AS a
USING "Organizations" AS o
WHERE a."OrganizationId" = o."Id"
	AND o."AuditRetentionDays" > 0
	AND a."Date" < now() - make_interval(days => o."AuditRetentionDays");