from ai_maintenance.scanner import scan_repo
from ai_maintenance.azure_ai_engine import analyze_with_azure_ai
from ai_maintenance.report import generate_report

REPO_PATH = "."

files = scan_repo(REPO_PATH)

print("\n========= AI TEST MAINTENANCE (AZURE AI) =========\n")

for file in files:
    analysis = analyze_with_azure_ai(file["path"], file["content"])
    generate_report(file["path"], analysis)