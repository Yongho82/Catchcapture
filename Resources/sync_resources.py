import re
import os
import sys

def sync_resources(target_lang='ja'):
    # 경로 설정
    base_dir = os.path.dirname(os.path.abspath(__file__))
    en_path = os.path.join(base_dir, 'Strings.en.resx')
    target_path = os.path.join(base_dir, f'Strings.{target_lang}.resx')
    output_path = os.path.join(base_dir, f'missing_{target_lang}.txt')

    if not os.path.exists(en_path):
        print(f"Error: {en_path} not found.")
        return

    if not os.path.exists(target_path):
        print(f"Error: {target_path} not found.")
        return

    print(f"Comparing: en vs {target_lang}")

    # 파일 읽기 (UTF-8)
    with open(en_path, 'r', encoding='utf-8') as f:
        en_content = f.read()
    with open(target_path, 'r', encoding='utf-8') as f:
        target_content = f.read()

    # 모든 리소스 키 추출 (Regex)
    # <data name="KeyName" ...> 형태 탐색
    en_keys = re.findall(r'name="([^"]+)"', en_content)
    target_keys = set(re.findall(r'name="([^"]+)"', target_content))

    # 누락된 키 찾기
    missing_keys = [k for k in en_keys if k not in target_keys]

    if not missing_keys:
        print(f"All resources are in sync for {target_lang}!")
        return

    print(f"Found {len(missing_keys)} missing resources.")

    # 누락된 데이터 블록 추출
    missing_blocks = []
    for key in missing_keys:
        # <data name="key"> ... </data> 블록 전체를 매칭 (멀티라인 포함)
        pattern = fr'(?s)([ ]*<data name="{re.escape(key)}".*? </data>)'
        match = re.search(pattern, en_content)
        if match:
            missing_blocks.append(match.group(1))

    # 결과 저장
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write('\n'.join(missing_blocks))

    print(f"Successfully created: {output_path}")
    print("-" * 30)
    for k in missing_keys:
        print(f" [+] {k}")

if __name__ == "__main__":
    # 기본적으로 ja(일본어)를 비교하며, 인자로 다른 언어를 줄 수도 있습니다.
    # 예: python sync_resources.py ar
    lang = sys.argv[1] if len(sys.argv) > 1 else 'ja'
    sync_resources(lang)
