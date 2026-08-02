# Google Forms & Sheets setup

The Certificate Engine has no registration UI of its own. **Google Forms is the intake UI**, and the **Google Sheet linked to that Form is the source of truth** for every submission. This guide covers everything needed to connect the engine to a Form/Sheet: the GCP project and service account, sharing the Sheet, the column naming convention, the approval workflow, optional quiz scoring, and an optional low-latency push from Apps Script.

## 1. Create a Google Cloud project

1. Go to [console.cloud.google.com](https://console.cloud.google.com) and create a new project (or reuse an existing one dedicated to the NGO's internal tools).
2. You'll come back to this project in the next two steps to enable the API and create the service account.

## 2. Enable the Google Sheets API

1. In the Cloud Console, go to **APIs & Services → Library**.
2. Search for **Google Sheets API** and click **Enable**.
3. No other Google API needs to be enabled — the engine only ever talks to Sheets.

## 3. Create a service account

1. Go to **APIs & Services → Credentials → Create Credentials → Service account**.
2. Give it a descriptive name, e.g. `certificate-engine`.
3. You do **not** need to grant it any project-level IAM role. Access to the Sheet is granted by sharing the Sheet directly with the service account's email (step 5), not through IAM.
4. Once created, open the service account, go to the **Keys** tab, click **Add key → Create new key**, choose **JSON**, and download it. Treat this file like a password — it grants read/write access to every Sheet shared with it.

The downloaded JSON contains a `client_email` field that looks like:

```
certificate-engine@your-project-id.iam.gserviceaccount.com
```

You'll need this exact address in step 5.

## 4. Point the app at the credential

The engine resolves Google credentials in this order (first one found wins):

1. `GoogleSheets:ServiceAccountJson` — the raw JSON content as a config value (useful with a secret manager that injects whole values).
2. The environment variable named by `GoogleSheets:ServiceAccountJsonEnvironmentVariable` (default `GOOGLE_SERVICE_ACCOUNT_JSON`) — set this to the raw JSON content.
3. A file at `GoogleSheets:ServiceAccountJsonPath`.
4. A file at the path in the environment variable named by `GoogleSheets:ServiceAccountPathEnvironmentVariable` (default `GOOGLE_APPLICATION_CREDENTIALS`).

For a systemd deployment, the simplest option is usually to copy the JSON key file to the server (e.g. `/etc/certificate-engine/service-account.json`, readable only by the service user) and set:

```
GoogleSheets__ServiceAccountJsonPath=/etc/certificate-engine/service-account.json
```

in `/etc/certificate-engine/secrets.env` (see [`docs/hosting.md`](hosting.md)). Never commit the JSON key to git — it's already covered by this repo's `.gitignore` (`service-account*.json`).

## 5. Share the Sheet with the service account

1. Open the Google Sheet linked to the Form (Form → Responses tab → the green Sheets icon, or **View responses in Sheets**).
2. Click **Share**, paste in the service account's `client_email` from step 3, set its role to **Editor**, and uncheck "Notify people" (it's a machine account, not a person).
3. Editor access is required because the engine writes the `Certificate Status`, `Verification ID`, and `Certificate Processed At` columns back into the Sheet, in addition to reading rows.

Repeat this for every Sheet/event you configure in `Platform:Events`. The `SpreadsheetId` you'll need for that config is the long ID segment in the Sheet's URL: `https://docs.google.com/spreadsheets/d/<SPREADSHEET_ID>/edit`.

## 6. Form → Sheet column naming convention

By default, a Google Form's response Sheet has one column per question plus a `Timestamp` column, on a tab named exactly `Form Responses 1` (which is where the `SheetName` default comes from — if you rename the tab, update `SheetName` to match). The engine expects (by default) the columns below, matching `EventSourceOptions`:

| Config key | Default column header | Purpose |
| --- | --- | --- |
| `SourceIdColumn` | `Timestamp` | Natural unique key per submission (Forms fills this in automatically and it never repeats). |
| `NameColumn` | `Full Name` | Participant's name, printed on the certificate. |
| `EmailColumn` | `Email Address` | Delivery address for email. Tip: turn on **Collect email addresses** in Form settings so this is populated automatically and validated at submission time. |
| `PhoneColumn` | `Phone Number` | Delivery number for WhatsApp (include country code). |
| `ApprovalColumn` | `Status` | Staff-controlled approval gate (see below). |
| `ScoreColumn` | *(none by default)* | Optional; used only if you're gating on quiz score (see §8). |
| `CertificateStatusColumn` | `Certificate Status` | **Written by the engine.** Don't edit by hand. |
| `VerificationIdColumn` | `Verification ID` | **Written by the engine.** The certificate's public verification ID. |
| `ProcessedAtColumn` | `Certificate Processed At` | **Written by the engine.** UTC timestamp of processing. |

You can rename any of these headers to match your organization's existing Form — just update the matching key under that event in `Platform:Events` so the engine looks at the right column. The engine matches columns by header text in row 1, not by position, so columns can be reordered or new Form questions inserted freely.

Before staff start approving rows, manually add empty columns for `Status`, `Certificate Status`, `Verification ID`, and `Certificate Processed At` to the response Sheet (Google Forms won't create these on its own, since they aren't Form questions).

## 7. The approval workflow

1. A participant submits the Form. A new row appears in the Sheet with `Status` blank.
2. NGO staff review the row and type `Approved` into the `Status` column (must match `ApprovalValue` for that event exactly — case-sensitive, default `"Approved"`).
3. On its next poll (every `PollIntervalSeconds`, default ~90s), the engine reads all rows for that event's Sheet/Range and processes any row where `ApprovalColumn` equals `ApprovalValue` **and** `CertificateStatusColumn` is still empty (so a row is only ever processed once).
4. If issuance and delivery succeed, the engine writes:
   - `Certificate Status` → e.g. `Issued` (or a failure/pending status if something went wrong — check this column first when troubleshooting a missing certificate).
   - `Verification ID` → the certificate's public ID, i.e. the same value used in `/verify/{publicId}`.
   - `Certificate Processed At` → the UTC timestamp of processing.
5. If a row didn't process the way you expected, check `Certificate Status`, the engine's logs, and `GET /internal/audit/verify` before manually editing the Sheet — clearing `Certificate Status` to force a retry may or may not be supported depending on how the current build handles re-processing, so confirm with whoever is maintaining `src/CertificateEngine` if you need to force a re-issue.

## 8. Optional: Google Forms quiz scoring

If certificates should only be issued to participants who pass a quiz embedded in the Form:

1. In the Form editor, click the gear icon → **Settings → Quizzes** → turn on **Make this a quiz**.
2. For each question, click **Answer key**, mark the correct answer(s), and assign a point value.
3. Once the quiz has at least one response, the linked Sheet gains a `Score` column showing each respondent's result (typically formatted like `8 / 10`).
4. Set `ScoreColumn` (e.g. `"Score"`) and `MinimumScore` on that event in `Platform:Events`. The engine will only issue a certificate for rows meeting both the score gate and the normal `Status = Approved` gate.
5. If Google's `Score` column format (e.g. `8 / 10`) doesn't parse the way you expect, add a small helper column with a formula that extracts just the numeric value (e.g. `=VALUE(REGEXEXTRACT(H2, "^\d+"))`) and point `ScoreColumn` at that helper column instead.

Quiz scoring and the `Status` approval column are independent — you still need staff to set `Status` to `Approved`, even for participants who pass the quiz. This keeps a human in the loop before anything is issued.

## 9. Optional: Apps Script push for lower latency

Polling alone (default every ~90 seconds) is sufficient for typical NGO event volumes. If you want certificates to go out faster after a form submission, you can add a small Apps Script bound to the Sheet that calls the internal `poll-now` endpoint as soon as a response comes in.

> **Security note:** the internal API is meant to stay off the public internet (see [`docs/hosting.md`](hosting.md) — the reference Caddy config actively blocks `/internal/*`). Using this optional push means deliberately exposing **just** the `poll-now` route through your reverse proxy, protected by the `X-Api-Key` header. If that's more exposure than you're comfortable with, skip this section — polling alone works fine.

1. Open the Sheet, then **Extensions → Apps Script**.
2. Paste in:

   ```javascript
   function onFormSubmit(e) {
     var eventId = 'YOUR_EVENT_ID';           // matches Platform:Events[].EventId
     var url = 'https://your-internal-host/internal/events/' + eventId + '/poll-now';

     var options = {
       method: 'post',
       contentType: 'application/json',
       headers: {
         'X-Api-Key': 'THE_SHARED_INTERNAL_API_KEY'
       },
       muteHttpExceptions: true
     };

     var response = UrlFetchApp.fetch(url, options);
     Logger.log(response.getResponseCode() + ' ' + response.getContentText());
   }
   ```

3. Replace `YOUR_EVENT_ID` and the API key, and `your-internal-host` with whatever narrowly-scoped hostname/path you've exposed for this purpose.
4. Click the clock icon (**Triggers**) → **Add Trigger** → function `onFormSubmit`, event source **From spreadsheet**, event type **On form submit**.
5. If more than one person can edit the script, store the API key using Apps Script's project properties instead of hardcoding it in shared source.

Even with this push enabled, keep polling on — it's the reliable fallback if the push ever fails silently (e.g., quota errors, trigger misconfiguration).
