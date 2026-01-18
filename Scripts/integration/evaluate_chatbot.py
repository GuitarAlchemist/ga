import json
import requests
import os
import sys
from datetime import datetime

# Configuration
CHATBOT_API = "http://localhost:7001/api/chat"
OLLAMA_API = "http://localhost:11434/api/generate"
JUDGE_MODEL = "llama3.2"
DATA_FILE = r"..\..\Tests\Data\groundedness_bench.jsonl"
REPORT_FILE = "evaluation_report.md"

def call_chatbot(message):
    try:
        resp = requests.post(CHATBOT_API, json={"Message": message, "SessionId": "eval-run"}, timeout=30)
        resp.raise_for_status()
        return resp.json()
    except Exception as e:
        return {"error": str(e)}

def call_judge(input_text, actual_output, expected_criteria):
    prompt = f"""You are an expert impartial judge evaluating an AI music assistant.
    
    [User Request]: {input_text}
    [Expected Criteria]: {json.dumps(expected_criteria)}
    [Actual AI Response]: {actual_output}
    
    Task: specificially check if the 'Actual AI Response' satisfies the 'Expected Criteria'.
    - If criteria mentions "identified_chord", the name must appear.
    - If "must_not_hallucinate", check for invented terms.
    - If "progression", check if a structured progression attachment was returned (hint: look for JSON structure or text indicating attachment).
    
    Respond with JSON only: {{ "pass": true/false, "reason": "short explanation" }}
    """
    
    try:
        resp = requests.post(OLLAMA_API, json={
            "model": JUDGE_MODEL, 
            "prompt": prompt, 
            "stream": False,
            "format": "json" 
        }, timeout=30)
        resp.raise_for_status()
        return json.loads(resp.json()['response'])
    except Exception as e:
        return {"pass": False, "reason": f"Judge Error: {str(e)}"}

def main():
    print(f"Starting Evaluation using {DATA_FILE}...")
    
    if not os.path.exists(DATA_FILE):
        print(f"Error: {DATA_FILE} not found.")
        return

    results = []
    
    with open(DATA_FILE, 'r') as f:
        lines = f.readlines()

    print(f"Found {len(lines)} test cases.")
    
    pass_count = 0
    
    for i, line in enumerate(lines):
        if not line.strip(): continue
        case = json.loads(line)
        id = case.get('id', f'Q{i}')
        input_text = case['input']
        expected = case['expected']
        
        print(f"Evaluating {id}...", end='', flush=True)
        
        # 1. Get Chatbot Response
        chat_resp = call_chatbot(input_text)
        
        if "error" in chat_resp:
            print(" [API ERROR]")
            results.append({
                "id": id,
                "input": input_text,
                "output": "API ERROR",
                "pass": False,
                "reason": chat_resp["error"]
            })
            continue

        actual_text = chat_resp.get('naturalLanguageAnswer', '')
        # Include attachment info in what the Judge sees
        if chat_resp.get('progression'):
            actual_text += f"\n[ATTACHMENT: Progression '{chat_resp['progression'].get('name')}']"
        
        # 2. Judge Response
        verdict = call_judge(input_text, actual_text, expected)
        
        if verdict.get('pass'):
            print(" [PASS]")
            pass_count += 1
        else:
            print(" [FAIL]")
            
        results.append({
            "id": id,
            "input": input_text,
            "output": actual_text,
            "pass": verdict.get('pass', False),
            "reason": verdict.get('reason', 'No reason provided')
        })

    # Generate Report
    score = (pass_count / len(lines)) * 100
    
    with open(REPORT_FILE, 'w', encoding='utf-8') as f:
        f.write(f"# Chatbot Evaluation Report\n")
        f.write(f"**Date:** {datetime.now()}\n")
        f.write(f"**Score:** {pass_count}/{len(lines)} ({score:.1f}%)\n\n")
        f.write("| ID | Input | Pass | Reason |\n")
        f.write("|----|-------|------|--------|\n")
        for r in results:
            status = "✅" if r['pass'] else "❌"
            clean_input = r['input'].replace('\n', ' ').replace('|', ' ')[:50]
            clean_reason = r['reason'].replace('\n', ' ').replace('|', ' ')
            f.write(f"| {r['id']} | {clean_input}... | {status} | {clean_reason} |\n")
            
    print(f"\nEvaluation Complete. Score: {score:.1f}%")
    print(f"Report saved to {REPORT_FILE}")

if __name__ == "__main__":
    main()
