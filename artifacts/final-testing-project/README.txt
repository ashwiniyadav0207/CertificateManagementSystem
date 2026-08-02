LOCAL TESTING PACKAGE
=====================

1. Double-click Start-Local-Testing.cmd.
2. The public Verification Website opens at http://localhost:5001.
3. The private Certificate Engine management landing page is at http://localhost:5000.

For a test certificate, send a POST request to:
  http://localhost:5000/internal/demo/issue

Header:
  X-Api-Key: local-testing-key-change-before-production-2026

Example JSON body:
  {"fullName":"Test Participant","eventName":"Local Testing Event"}

The response includes verifyPath. Open that path on http://localhost:5001.

This package is for local testing only. Configure a strong API key, Google Sheets,
SMTP/WhatsApp, signing PFX, and an HTTPS public URL before production deployment.
