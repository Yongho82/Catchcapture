using System.Collections.Generic;

namespace CatchCapture
{
    public class PrivacyPolicyData
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Intro { get; set; } = string.Empty;
        public string HighlightTitle { get; set; } = string.Empty;
        public string HighlightText { get; set; } = string.Empty;
        public List<(string Title, string Body)> Sections { get; set; } = new();
        public string ContactTitle { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public string CloseLabel { get; set; } = string.Empty;
    }

    public static class PrivacyPolicyContent
    {
        public static PrivacyPolicyData Get(string lang)
        {
            return lang switch
            {
                "en" => GetEn(),
                "zh" => GetZh(),
                "ja" => GetJa(),
                _ => GetKo(),
            };
        }

        private static PrivacyPolicyData GetKo()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture 개인정보 처리방침",
                Subtitle = "최종 수정일: 2025년 12월 4일",
                Intro = "이지업소프트(이하 \"회사\")는 개인정보 보호법 등 관련 법령을 준수하며, 이용자의 개인정보를 보호하기 위해 최선을 다하고 있습니다.",
                HighlightTitle = "【 중요 】",
                HighlightText = "CatchCapture는 개인정보를 수집하지 않습니다.\n본 앱은 사용자의 컴퓨터에서 로컬로 실행되며, 어떠한 개인정보도 외부 서버로 전송하지 않습니다.",
                Sections = new List<(string, string)>
                {
                    ("1. 개인정보 수집 및 이용",
                     "CatchCapture는 다음과 같은 정보를 수집하지 않습니다:\n• 이름, 이메일, 전화번호 등 개인 식별 정보\n• 위치 정보\n• 사용자 계정 정보\n• 네트워크 또는 인터넷 사용 기록"),
                    ("2. 로컬 데이터 저장",
                     "앱 설정 및 캡처된 이미지는 사용자의 컴퓨터에만 저장되며, 회사 서버나 제3자에게 전송되지 않습니다.\n\n저장 위치: %LocalAppData%\\CatchCapture"),
                    ("3. 제3자 제공 및 처리위탁",
                     "개인정보를 수집하지 않으므로 제3자 제공 또는 처리위탁이 없습니다."),
                    ("4. 이용자 권리",
                     "이용자는 언제든지 앱을 삭제하여 로컬에 저장된 모든 데이터를 제거할 수 있습니다."),
                    ("5. 안전성 확보 조치",
                     "모든 데이터는 사용자의 컴퓨터에서만 처리되며, Windows 운영체제의 보안 정책에 따라 보호됩니다."),
                },
                ContactTitle = "문의 및 연락처",
                ContactInfo = "회사명: 이지업소프트\n제품명: CatchCapture\n이메일: eyh1982@gmail.com\n웹사이트: https://ezupsoft.com\n시행일: 2025년 12월 4일",
                CloseLabel = "닫기"
            };
        }

        private static PrivacyPolicyData GetEn()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture Privacy Policy",
                Subtitle = "Last updated: Dec 4, 2025",
                Intro = "EZUPSOFT (\"we\") complies with applicable privacy laws and is committed to protecting users' personal data.",
                HighlightTitle = "【 IMPORTANT 】",
                HighlightText = "CatchCapture does not collect any personal information.\nThe app runs locally on your computer and does not send any personal data to external servers.",
                Sections = new List<(string, string)>
                {
                    ("1. Collection and Use of Personal Information",
                     "CatchCapture does not collect the following information:\n• Personal identifiers (name, email, phone)\n• Location information\n• User account information\n• Network or internet activity"),
                    ("2. Local Data Storage",
                     "App settings and captured images are stored only on your computer and are not transmitted to our servers or third parties.\n\nStorage path: %LocalAppData%\\CatchCapture"),
                    ("3. Third-Party Sharing and Processing",
                     "Since no personal information is collected, there is no sharing with or outsourcing to third parties."),
                    ("4. User Rights",
                     "You may delete the app at any time to remove all data stored locally."),
                    ("5. Security Measures",
                     "All data is processed only on your computer and protected by Windows security policies."),
                },
                ContactTitle = "Contact",
                ContactInfo = "Company: EZUPSOFT\nProduct: CatchCapture\nEmail: eyh1982@gmail.com\nWebsite: https://ezupsoft.com\nEffective date: Dec 4, 2025",
                CloseLabel = "Close"
            };
        }

        private static PrivacyPolicyData GetZh()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture 隐私政策",
                Subtitle = "最后更新：2025年12月4日",
                Intro = "EZUPSOFT（以下简称“公司”）遵守相关法律法规，致力于保护用户的个人信息。",
                HighlightTitle = "【 重要 】",
                HighlightText = "CatchCapture 不收集任何个人信息。\n本应用在您的电脑本地运行，不会将任何个人信息发送至外部服务器。",
                Sections = new List<(string, string)>
                {
                    ("1. 个人信息的收集与使用",
                     "CatchCapture 不会收集以下信息：\n• 个人身份信息（姓名、邮箱、电话）\n• 位置信息\n• 用户账户信息\n• 网络或互联网使用记录"),
                    ("2. 本地数据存储",
                     "应用设置和捕获的图像仅存储在您的电脑上，不会传输至公司服务器或第三方。\n\n存储位置：%LocalAppData%\\CatchCapture"),
                    ("3. 向第三方提供与委托处理",
                     "由于不收集个人信息，因此不存在向第三方提供或委托处理的情况。"),
                    ("4. 用户权利",
                     "用户可以随时卸载应用，以删除存储在本地的所有数据。"),
                    ("5. 安全措施",
                     "所有数据仅在您的电脑上处理，并遵循 Windows 操作系统的安全策略进行保护。"),
                },
                ContactTitle = "联系方式",
                ContactInfo = "公司：EZUPSOFT\n产品：CatchCapture\n邮箱：eyh1982@gmail.com\n网站：https://ezupsoft.com\n生效日期：2025年12月4日",
                CloseLabel = "关闭"
            };
        }

        private static PrivacyPolicyData GetJa()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture プライバシーポリシー",
                Subtitle = "最終更新日: 2025年12月4日",
                Intro = "EZUPSOFT（以下「当社」）は関連法令を遵守し、ユーザーの個人情報保護に努めています。",
                HighlightTitle = "【 重要 】",
                HighlightText = "CatchCapture は個人情報を収集しません。\n本アプリはユーザーのPC上でローカルに動作し、個人情報を外部サーバーへ送信しません。",
                Sections = new List<(string, string)>
                {
                    ("1. 個人情報の収集・利用",
                     "CatchCapture は以下の情報を収集しません：\n• 氏名、メール、電話番号などの識別情報\n• 位置情報\n• ユーザーアカウント情報\n• ネットワークまたはインターネットの利用記録"),
                    ("2. ローカルデータ保存",
                     "アプリの設定およびキャプチャ画像はユーザーのPCにのみ保存され、当社サーバーや第三者へ送信されません。\n\n保存場所: %LocalAppData%\\CatchCapture"),
                    ("3. 第三者提供・委託",
                     "個人情報を収集しないため、第三者提供や委託処理はありません。"),
                    ("4. 利用者の権利",
                     "ユーザーはいつでもアプリを削除して、ローカルに保存されているすべてのデータを削除できます。"),
                    ("5. 安全対策",
                     "すべてのデータはユーザーのPC上でのみ処理され、Windowsのセキュリティポリシーにより保護されます。"),
                },
                ContactTitle = "お問い合わせ",
                ContactInfo = "会社名：EZUPSOFT\n製品名：CatchCapture\nメール：eyh1982@gmail.com\nウェブサイト：https://ezupsoft.com\n施行日：2025年12月4日",
                CloseLabel = "閉じる"
            };
        }
    }
}
