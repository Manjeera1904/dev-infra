#!/usr/bin/env bash
set +e

PROJECT="Commercialization"
REPO=$1
ORG="EclipseInsightsHC"
TOKEN=$2

ROOT_DIR=$(pwd)

echo " Scanning repo: $REPO"

# Clone repo
git clone https://$TOKEN@dev.azure.com/$ORG/$PROJECT/_git/$REPO
cd $REPO || exit 0

# Install Trivy correctly (ONLY once)
if ! command -v trivy &>/dev/null; then
  echo "Installing Trivy..."
  curl -sfL https://raw.githubusercontent.com/aquasecurity/trivy/main/contrib/install.sh | sh
  sudo mv ./bin/trivy /usr/local/bin/trivy
fi

# Verify Trivy
trivy --version || exit 1

# Run vulnerability scan
trivy fs . \
  --scanners vuln \
  --severity HIGH,CRITICAL \
  --quiet \
  --format json \
  --output scan-results.json

# List vulnerabilities for this repo
export TARGET_REPO=$REPO

python "$ROOT_DIR/.azure-pipelines/maintenance/scripts/security/list_vulnerabilities.py"
python "$ROOT_DIR/.azure-pipelines/maintenance/scripts/security/manage_bug.py"

cd "$ROOT_DIR"
