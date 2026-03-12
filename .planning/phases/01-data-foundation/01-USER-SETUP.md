# Phase 01: User Setup Required

**Generated:** 2026-03-12
**Phase:** 01-data-foundation
**Status:** Incomplete

Complete these items before running real imports or external integrations outside the test environment.

## Environment Variables

| Status | Variable | Source | Add to |
|--------|----------|--------|--------|
| [ ] | `Oracle__ConnectionString` | Oracle connection string / credentials for the real source environment | Local machine environment or deployment profile |
| [ ] | `SqlServer__ConnectionString` | Local SQL Server Express connection details for the target machine | Local machine environment or deployment profile |
| [ ] | `Twilio__AccountSid` | Twilio Console → Account Info → Account SID | Local machine environment or deployment profile |
| [ ] | `Twilio__AuthToken` | Twilio Console → Account Info → Auth Token | Local machine environment or deployment profile |
| [ ] | `Twilio__MessagingServiceSid` | Twilio Console → Messaging → Services → Service SID | Local machine environment or deployment profile |
| [ ] | `Aws__AccessKeyId` | AWS Console / IAM → Access keys | Local machine environment or deployment profile |
| [ ] | `Aws__SecretAccessKey` | AWS Console / IAM → Secret access key | Local machine environment or deployment profile |
| [ ] | `Aws__Region` | AWS Console → Region selection | Local machine environment or deployment profile |

## Dashboard Configuration

- [ ] **Confirm the Oracle import query and privileges**
  - Location: Oracle source environment
  - Set to: A read-only account that can execute the configured import query
  - Notes: No schema changes are required in Oracle for this phase.

- [ ] **Confirm SQL Server Express connectivity**
  - Location: Local machine SQL Server configuration
  - Set to: Reachable instance and database creation permissions for the PenelopeSMS app
  - Notes: Later migrations and import runs depend on this connection succeeding locally.

- [ ] **Verify Twilio and AWS credentials are available for later phases**
  - Location: Twilio Console and AWS Console
  - Set to: Existing approved messaging resources and callback pipeline credentials
  - Notes: Phase 1 bootstrap does not call these services yet, but later phases will.

## Verification

After completing setup, verify with:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" test PenelopeSMS.sln
```

Expected results:
- The solution builds and tests pass with your local configuration in place.
- The app can read environment-backed settings without source edits.

---

**Once all items complete:** Mark status as "Complete" at top of file.
