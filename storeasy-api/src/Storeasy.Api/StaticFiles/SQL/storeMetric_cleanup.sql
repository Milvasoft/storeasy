DELETE FROM "StoreMetrics"
WHERE "Date" < (now() - interval '25 months')::date;