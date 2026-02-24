import os

SUPPORTED_EXTENSIONS = (".py", ".java", ".js", ".ts", ".yml", ".yaml")

def scan_repo(repo_path):
    files = []

    for root, _, filenames in os.walk(repo_path):
        for file in filenames:
            if file.endswith(SUPPORTED_EXTENSIONS):
                full_path = os.path.join(root, file)

                try:
                    with open(full_path, "r", encoding="utf-8", errors="ignore") as f:
                        files.append({
                            "path": full_path,
                            "content": f.read()
                        })
                except:
                    pass

    return files