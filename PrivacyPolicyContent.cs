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
                "zh-TW" => GetZh(), // Traditional uses same content for now or update if needed
                "ja" => GetJa(),
                "es" => GetEs(),
                "fr" => GetFr(),
                "de" => GetDe(),
                _ => GetKo(),
            };
        }

        private static PrivacyPolicyData GetKo()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture 개인정보 처리방침",
                Subtitle = "최종 수정일: 2026년 1월 14일",
                Intro = "이지업소프트(이하 \"회사\")는 『개인정보 보호법』 등 관련 법령을 준수하며, 이용자의 개인정보를 보호하고 이와 관련한 고충을 신속하고 원활하게 처리할 수 있도록 하기 위하여 다음과 같이 개인정보 처리방침을 수립•공개합니다.",
                HighlightTitle = "【 핵심 요약 】",
                HighlightText = "CatchCapture는 어떠한 형태의 개인정보도 수집하지 않습니다.\n본 소프트웨어는 사용자의 PC(로컬 환경)에서만 독립적으로 실행되며, 사용자가 생성한 데이터(이미지, 설정 등)를 외부 서버로 전송하지 않습니다.",
                Sections = new List<(string, string)>
                {
                    ("1. 개인정보의 수집 및 보유",
                     "회사는 본 소프트웨어를 통해 이용자의 어떠한 개인정보(성명, 연락처, 이메일, 기기정보, 위치정보 등)도 수집, 저장, 또는 보유하지 않습니다.\n\n따라서 별도의 회원가입 절차가 없으며, 이용자의 사용 기록 역시 회사는 접근할 수 없습니다."),
                    ("2. 데이터의 저장 및 관리",
                     "소프트웨어 이용 중 생성되는 모든 데이터(캡처 이미지, 녹화 영상, 설정 값, 클립보드 내역 등)는 이용자의 PC 내에만 저장됩니다.\n\n• 저장 경로: %LocalAppData%\\CatchCapture (또는 사용자가 지정한 폴더)\n• 관리 책임: 데이터에 대한 관리 및 백업 책임은 이용자 본인에게 있습니다."),
                    ("3. 키보드 및 마우스 입력 정보의 처리",
                     "본 소프트웨어는 '화면 캡처' 및 '단축키 실행' 기능을 구현하기 위해 운영체제의 입력 이벤트(키보드 후킹 등)를 감지할 수 있습니다.\n하지만 이러한 정보는 오직 해당 기능을 수행하기 위한 목적으로만 실시간으로 사용되며, 별도로 저장되거나 외부로 전송되지 않습니다."),
                    ("4. 제3자 제공 및 위탁",
                     "회사는 개인정보를 수집하지 않으므로, 제3자에게 개인정보를 제공하거나 처리를 위탁하는 사실이 없습니다."),
                    ("5. 이용자의 권리 및 행사 방법",
                     "이용자는 언제든지 본 소프트웨어를 삭제(인스톨 제거)함으로써 로컬에 저장된 애플리케이션 데이터를 파기할 수 있습니다. 사용자가 직접 저장한 이미지 파일은 별도로 삭제해야 합니다."),
                    ("6. 개인정보 보호책임자 및 담당부서",
                     "회사는 개인정보 처리에 관한 업무를 총괄해서 책임지고, 이와 관련한 이용자의 불만처리 및 피해구제 등을 위하여 아래와 같이 책임자를 지정하고 있습니다."),
                },
                ContactTitle = "7. 문의처",
                ContactInfo = "회사명: 이지업소프트\n관련 문의: ezupsoft@gmail.com\n웹사이트: https://ezupsoft.com",
                CloseLabel = "닫기"
            };
        }

        private static PrivacyPolicyData GetEn()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture Privacy Policy",
                Subtitle = "Last updated: Jan 14, 2026",
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
                ContactInfo = "Company: EZUPSOFT\nProduct: CatchCapture\nEmail: ezupsoft@gmail.com\nWebsite: https://ezupsoft.com\nEffective date: Jan 14, 2026",
                CloseLabel = "Close"
            };
        }

        private static PrivacyPolicyData GetZh()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture 隐私政策",
                Subtitle = "最后更新：2026年1月14日",
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
                ContactInfo = "公司：EZUPSOFT\n产品：CatchCapture\n邮箱：ezupsoft@gmail.com\n网站：https://ezupsoft.com\n生效日期：2026年1月14日",
                CloseLabel = "关闭"
            };
        }

        private static PrivacyPolicyData GetJa()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture プライバシーポリシー",
                Subtitle = "最終更新日: 2026年1月14日",
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
                ContactInfo = "会社名：EZUPSOFT\n製品名：CatchCapture\nメール：ezupsoft@gmail.com\nウェブサイト：https://ezupsoft.com\n施行日：2026年1月14日",
                CloseLabel = "閉じる"
            };
        }

        private static PrivacyPolicyData GetEs()
        {
            return new PrivacyPolicyData
            {
                Title = "Política de Privacidad de CatchCapture",
                Subtitle = "Última actualización: 14 de ene. de 2026",
                Intro = "EZUPSOFT (\"nosotros\") cumple con las leyes de privacidad aplicables y se compromete a proteger los datos personales de los usuarios.",
                HighlightTitle = "【 IMPORTANTE 】",
                HighlightText = "CatchCapture no recopila ninguna información personal.\nLa aplicación se ejecuta localmente en su computadora y no envía ningún dato personal a servidores externos.",
                Sections = new List<(string, string)>
                {
                    ("1. Recopilación y Uso de Información Personal",
                     "CatchCapture no recopila la siguiente información:\n• Identificadores personales (nombre, correo electrónico, teléfono)\n• Información de ubicación\n• Información de la cuenta de usuario\n• Actividad de red o internet"),
                    ("2. Almacenamiento de Datos Local",
                     "La configuración de la aplicación y las imágenes capturadas se almacenan solo en su computadora y no se transmiten a nuestros servidores ni a terceros.\n\nRuta de almacenamiento: %LocalAppData%\\CatchCapture"),
                    ("3. Intercambio y Procesamiento con Terceros",
                     "Dado que no se recopila información personal, no hay intercambio ni subcontratación con terceros."),
                    ("4. Derechos del Usuario",
                     "Puede eliminar la aplicación en cualquier momento para eliminar todos los datos almacenados localmente."),
                    ("5. Medidas de Seguridad",
                     "Todos los datos se procesan solo en su computadora y están protegidos por las políticas de seguridad de Windows."),
                },
                ContactTitle = "Contacto",
                ContactInfo = "Empresa: EZUPSOFT\nProducto: CatchCapture\nCorreo electrónico: ezupsoft@gmail.com\nSitio web: https://ezupsoft.com\nFecha de vigencia: 14 de ene. de 2026",
                CloseLabel = "Cerrar"
            };
        }

        private static PrivacyPolicyData GetFr()
        {
            return new PrivacyPolicyData
            {
                Title = "Politique de Confidentialité de CatchCapture",
                Subtitle = "Dernière mise à jour : 14 janv. 2026",
                Intro = "EZUPSOFT (« nous ») respecte les lois applicables en matière de confidentialité et s'engage à protéger les données personnelles des utilisateurs.",
                HighlightTitle = "【 IMPORTANT 】",
                HighlightText = "CatchCapture ne collecte aucune information personnelle.\nL'application s'exécute localement sur votre ordinateur et n'envoie aucune donnée personnelle à des serveurs externes.",
                Sections = new List<(string, string)>
                {
                    ("1. Collecte et Utilisation des Informations Personnelles",
                     "CatchCapture ne collecte pas les informations suivantes :\n• Identifiants personnels (nom, e-mail, téléphone)\n• Informations de localisation\n• Informations de compte utilisateur\n• Activité réseau ou internet"),
                    ("2. Stockage Local des Données",
                     "Les paramètres de l'application et les images capturées sont stockés uniquement sur votre ordinateur et ne sont pas transmis à nos serveurs ou à des tiers.\n\nChemin de stockage : %LocalAppData%\\CatchCapture"),
                    ("3. Partage et Traitement par des Tiers",
                     "Puisqu'aucune information personnelle n'est collectée, il n'y a aucun partage ou sous-traitance avec des tiers."),
                    ("4. Droits de l'Utilisateur",
                     "Vous pouvez supprimer l'application à tout moment pour supprimer toutes les données stockées localement."),
                    ("5. Mesures de Sécurité",
                     "Toutes les données sont traitées uniquement sur votre ordinateur et protégées par les politiques de sécurité de Windows."),
                },
                ContactTitle = "Contact",
                ContactInfo = "Société : EZUPSOFT\nProduit : CatchCapture\nE-mail : ezupsoft@gmail.com\nSite web : https://ezupsoft.com\nDate d'entrée en vigueur : 14 janv. 2026",
                CloseLabel = "Fermer"
            };
        }

        private static PrivacyPolicyData GetDe()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture Datenschutzerklärung",
                Subtitle = "Zuletzt aktualisiert: 14. Jan. 2026",
                Intro = "EZUPSOFT („wir“) hält die geltenden Datenschutzgesetze ein und verpflichtet sich zum Schutz der personenbezogenen Daten der Nutzer.",
                HighlightTitle = "【 WICHTIG 】",
                HighlightText = "CatchCapture sammelt keine personenbezogenen Daten.\nDie App läuft lokal auf Ihrem Computer und sendet keine personenbezogenen Daten an externe Server.",
                Sections = new List<(string, string)>
                {
                    ("1. Erhebung und Nutzung personenbezogener Daten",
                     "CatchCapture sammelt folgende Informationen nicht:\n• Persönliche Identifikatoren (Name, E-Mail, Telefon)\n• Standortinformationen\n• Benutzerkontoinformationen\n• Netzwerk- oder Internetaktivitäten"),
                    ("2. Lokale Datenspeicherung",
                     "App-Einstellungen und erfasste Bilder werden nur auf Ihrem Computer gespeichert und nicht an unsere Server oder Dritte übertragen.\n\nSpeicherpfad: %LocalAppData%\\CatchCapture"),
                    ("3. Weitergabe an Dritte und Auftragsverarbeitung",
                     "Da keine personenbezogenen Daten erhoben werden, erfolgt keine Weitergabe an oder Auslagerung an Dritte."),
                    ("4. Benutzerrechte",
                     "Sie können die App jederzeit löschen, um alle lokal gespeicherten Daten zu entfernen."),
                    ("5. Sicherheitsmaßnahmen",
                     "Alle Daten werden nur auf Ihrem Computer verarbeitet und durch Windows-Sicherheitsrichtlinien geschützt."),
                },
                ContactTitle = "Kontakt",
                ContactInfo = "Firma: EZUPSOFT\nProdukt: CatchCapture\nE-Mail: ezupsoft@gmail.com\nWebseite: https://ezupsoft.com\nDatum des Inkrafttretens: 14. Jan. 2026",
                CloseLabel = "Schließen"
            };
        }
    }
}
