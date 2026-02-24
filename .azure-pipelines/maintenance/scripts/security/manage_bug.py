import json
import os
import sys
import base64
import requests
import re

def get_next_version(existing_titles, base_title):
    max_version = 0
    for title in existing_titles:
        match = re.search(rf"{re.escape(base_title)} v(\d+)$", title)
        if match:
            max_version = max(max_version, int(match.group(1)))
    return max_version + 1

# -------------------------------------------------
# Configuration
# -------------------------------------------------
ORG = "EclipseInsightsHC"
PROJECT = "Commercialization"
WORK_ITEM_TYPE = "Bug"

SCAN_FILE = "scan-results.json"
REPO = os.getenv("TARGET_REPO", "unknown-repo")
TOKEN = os.getenv("SYSTEM_ACCESSTOKEN")

if not TOKEN:
    print("❌ SYSTEM_ACCESSTOKEN missing")
    sys.exit(1)

# -------------------------------------------------
# Azure DevOps Auth (Pipeline OAuth Token)
# -------------------------------------------------
auth = base64.b64encode(f":{TOKEN}".encode()).decode()
BASE_URL = f"https://dev.azure.com/{ORG}/{PROJECT}/_apis"

HEADERS = {
    "Authorization": f"Basic {auth}",
    "Content-Type": "application/json-patch+json"
}
# -------------------------------------------------
# SET THIS after running diagnostic once
# Change to the correct field found by diagnose_bug_fields()
# e.g. "Microsoft.VSTS.TCM.ReproSteps" for Scrum
# -------------------------------------------------
REPRO_STEPS_FIELD = "Microsoft.VSTS.TCM.ReproSteps"

# -------------------------------------------------
# Find existing Bug
# -------------------------------------------------
def find_existing_bugs(cve_id):
    wiql = {
        "query": f"""
        SELECT [System.Id], [System.State], [System.Title]
        FROM WorkItems
        WHERE
          [System.TeamProject] = '{PROJECT}'
          AND [System.WorkItemType] = 'Bug'
          AND [System.Title] CONTAINS '{cve_id}'
          AND [System.Tags] CONTAINS '{REPO}'
        """
    }

    r = requests.post(
        f"{BASE_URL}/wit/wiql?api-version=7.0",
        headers={"Authorization": f"Basic {auth}"},
        json=wiql
    )

    if r.status_code != 200:
        return []

    bugs = []
    for item in r.json().get("workItems", []):
        bug = requests.get(
            f"{BASE_URL}/wit/workitems/{item['id']}?api-version=7.0",
            headers={"Authorization": f"Basic {auth}"}
        ).json()

        bugs.append({
            "id": item["id"],
            "state": bug["fields"].get("System.State", ""),
            "title": bug["fields"].get("System.Title", "")
        })

    return bugs


# -------------------------------------------------
# Create Bug
# -------------------------------------------------
def create_bug(title, description, repro_steps, priority, severity):
    payload = [
        {"op": "add", "path": "/fields/System.Title", "value": title},
        {"op": "add", "path": "/fields/System.Description", "value": description},
        {"op": "add", "path": f"/fields/{REPRO_STEPS_FIELD}", "value": f"<div>{repro_steps}</div>"},
        {"op": "add", "path": "/fields/Microsoft.VSTS.Common.Priority", "value": priority},
        {"op": "add", "path": "/fields/Microsoft.VSTS.Common.Severity", "value": severity},
        {"op": "add", "path": "/fields/System.Tags", "value": f"Security;Vulnerability;Trivy;{REPO}"}
    ]

    r = requests.post(
        f"{BASE_URL}/wit/workitems/$Bug?api-version=7.0",
        headers=HEADERS,
        json=payload
    )

    if r.status_code not in (200, 201):
        print("❌ Failed to create bug")
        print(r.text)
        return

    print(f"🐞 Created Bug ID: {r.json().get('id')}")

# -------------------------------------------------
# Update Bug
# -------------------------------------------------
def update_bug(bug_id, state, description, repro_steps):
    ops = [
        {"op": "add", "path": "/fields/System.Description", "value": description},
        {"op": "add", "path": f"/fields/{REPRO_STEPS_FIELD}", "value": f"<div>{repro_steps}</div>"}
    ]

    if state.lower() in ("closed", "resolved", "done"):
        ops.append(
            {"op": "add", "path": "/fields/System.State", "value": "Active"}
        )

    r = requests.patch(
        f"{BASE_URL}/wit/workitems/{bug_id}?api-version=7.0",
        headers=HEADERS,
        json=ops
    )
    print(f"🔄 Updated Bug ID: {bug_id}")

# -------------------------------------------------
# MAIN — Diagnose first, then process scan results
# -------------------------------------------------

# Step 1: Process scan file
if not os.path.exists(SCAN_FILE):
    print("ℹ scan-results.json not found — exiting after diagnostic.")
    sys.exit(0)

with open(SCAN_FILE) as f:
    data = json.load(f)

# Track CVEs already processed for this repo
processed_cves = set()

for result in data.get("Results", []):
    target = result.get("Target", "N/A")

    for v in result.get("Vulnerabilities", []) or []:
        severity = v.get("Severity")
        if severity not in ("HIGH", "CRITICAL"):
            continue

        cve = v.get("VulnerabilityID")

        # Skip duplicate CVEs in same repo
        if cve in processed_cves:
            continue

        processed_cves.add(cve)

        pkg = v.get("PkgName")
        priority = 1 if severity == "CRITICAL" else 2
        bug_severity = "1 - Critical" if severity == "CRITICAL" else "2 - High"

        vuln_title = (
            v.get("Title")
            or (v.get("Description", "").split(".")[0])
            or "Vulnerability detected"
        )

        title = f"[{cve} @ {REPO}]: {vuln_title}"

        # ---- Description
        description = (
            f"<b>Security vulnerability detected by automated Trivy scan.</b><br/><br/>"
            f"<b>Repository</b>: {REPO}<br/>"
            f"<b>CVE</b>: {cve}<br/>"
            f"<b>Severity</b>: {severity}<br/>"
        )

        # ---- Repro Steps
        repro_steps = (
            f"<b>Detected</b>: Trivy<br/>"
            f"<b>Affected</b>: {target}<br/><br/>"
            f"<b>Package Info</b><br/>"
            f"Package: {pkg}<br/>"
            f"Installed Version: {v.get('InstalledVersion')}<br/>"
            f"Fixed Version: {v.get('FixedVersion')}<br/><br/>"
            f"<b>Impact</b><br/>"
            f"{v.get('Description')}<br/><br/>"
            f"<b>Remediation</b><br/>"
            f"Upgrade {pkg} to a secure version.<br/><br/>"
            f"<b>References</b><br/>"
            f"<a href=\"{v.get('PrimaryURL')}\">{v.get('PrimaryURL')}</a>"
        )

        bugs = find_existing_bugs(cve)

        active_bug = next(
            (b for b in bugs if b["state"].lower() not in ("closed", "resolved", "done")),
            None
        )

        closed_bugs = [
            b for b in bugs if b["state"].lower() in ("closed", "resolved", "done")
        ]

        if not bugs:
            # No bug exists → create first bug
            create_bug(title, description, repro_steps, priority, bug_severity)

        elif active_bug:
            # Active bug exists → SKIP instead of update
            print(
                f"⏭️ Skipping CVE {cve} for repo {REPO} — "
                f"active Bug ID {active_bug['id']} already exists"
            )
            continue

        else:
            # Only closed bugs exist → create versioned bug
            existing_titles = [b["title"] for b in bugs]
            next_version = get_next_version(existing_titles, title)
            versioned_title = f"{title} v{next_version}"

            create_bug(
                versioned_title,
                description,
                repro_steps,
                priority,
                bug_severity
            )
