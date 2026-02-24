import json
import os
import sys

file_path = "scan-results.json"
repo = os.getenv("TARGET_REPO", "unknown-repo")

PRIORITY = 1
ACTIVITY = "Development"
TAGS = ["Vulnerability Scan", "Security", "Trivy"]

if not os.path.exists(file_path):
    sys.exit(0)

with open(file_path) as f:
    data = json.load(f)

def print_package_table(pkg, installed, fixed):
    print("+----------------+--------------------+------------------------------+")
    print("| Package        | Installed Version  | Fixed Version                |")
    print("+----------------+--------------------+------------------------------+")
    print(
        f"| {pkg:<14} | "
        f"{(installed or 'N/A'):<18} | "
        f"{(fixed or 'N/A'):<28} |"
    )
    print("+----------------+--------------------+------------------------------+")

for result in data.get("Results", []):
    target = result.get("Target", "N/A")
    scan_type = result.get("Type", "filesystem")

    for v in result.get("Vulnerabilities", []) or []:
        severity = v.get("Severity")

        if severity in ("HIGH", "CRITICAL"):
            cve = v.get("VulnerabilityID")
            title = v.get("Title") or "Security Vulnerability"
            pkg = v.get("PkgName")
            installed = v.get("InstalledVersion")
            fixed = v.get("FixedVersion")
            description = v.get("Description") or "No description provided."
            reference = v.get("PrimaryURL")

            print("\n" + "=" * 100)
            print(f"[{cve} @ {repo}]: {title}")
            print("=" * 100)

            # -----------------------------
            # Metadata
            # -----------------------------
            print("\nMetadata")
            print(f"Severity : {severity}")
            print(f"Priority : {PRIORITY}")
            print(f"Activity : {ACTIVITY}")
            print(f"Tags     : {', '.join(TAGS + [repo])}")
            print(f"Detected : Trivy")
            print(f"Affected : {scan_type} | {target}")

            # -----------------------------
            # Package Info (TABLE FORMAT)
            # -----------------------------
            print("\nPackage Info")
            print_package_table(pkg, installed, fixed)

            # -----------------------------
            # Impact (from scan)
            # -----------------------------
            print("\nImpact")
            print(description)

            # -----------------------------
            # Remediation
            # -----------------------------
            print("\nRemediation")
            if fixed:
                print(f"Upgrade {pkg} to version {fixed} or later.")
            else:
                print("Upgrade to a secure version when available.")

            # -----------------------------
            # Description
            # -----------------------------
            print("\nDescription")
            print(description)

            # -----------------------------
            # References
            # -----------------------------
            print("\nReferences")
            if reference:
                print(reference)
            else:
                print("No reference available.")

            print("\n" + "=" * 100 + "\n")
