import os
from openai import AzureOpenAI

client = AzureOpenAI(
    api_key=os.getenv("AZURE_OPENAI_API_KEY"),
    azure_endpoint=os.getenv("AZURE_OPENAI_ENDPOINT"),
    api_version="2024-02-15-preview"
)

def analyze_with_azure_ai(file_path, content):

    prompt = f"""
You are a Senior QA Automation Architect.

Analyze this automation test code for:
- Duplicate logic
- Hardcoded values
- Weak validation
- Missing assertions
- Dead code
- Refactoring opportunities
- Reusable component suggestions

File: {file_path}

Code:
{content[:12000]}
"""

    response = client.chat.completions.create(
        model=os.getenv("AZURE_DEPLOYMENT_NAME"),
        messages=[
            {"role": "system", "content": "You are a QA automation expert."},
            {"role": "user", "content": prompt}
        ],
        temperature=0.2
    )

    return response.choices[0].message.content