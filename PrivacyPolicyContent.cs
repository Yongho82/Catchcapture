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
            return lang.ToLower() switch
            {
                "en" => GetEn(),
                "zh" => GetZh(),
                "zh-tw" => GetZh(),  // 번체와 간체 동일하게 유지 (필요 시 별도 메서드 분리 가능)
                "ja" => GetJa(),
                "es" => GetEs(),
                "fr" => GetFr(),
                "de" => GetDe(),
                "ko" => GetKo(),
                "ar" => GetAr(),
                "id" => GetId(),
                "it" => GetIt(),
                "pt" => GetPt(),
                "ru" => GetRu(),
                "th" => GetTh(),
                "tr" => GetTr(),
                "vi" => GetVi(),
                _ => GetKo(),  // 기본값: 한국어
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
                Subtitle = "Last updated: January 14, 2026",
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
                ContactInfo = "Company: EZUPSOFT\nProduct: CatchCapture\nEmail: ezupsoft@gmail.com\nWebsite: https://ezupsoft.com\nEffective date: January 14, 2026",
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
                Subtitle = "Última actualización: 14 de enero de 2026",
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
                ContactInfo = "Empresa: EZUPSOFT\nProducto: CatchCapture\nCorreo electrónico: ezupsoft@gmail.com\nSitio web: https://ezupsoft.com\nFecha de vigencia: 14 de enero de 2026",
                CloseLabel = "Cerrar"
            };
        }

        private static PrivacyPolicyData GetFr()
        {
            return new PrivacyPolicyData
            {
                Title = "Politique de Confidentialité de CatchCapture",
                Subtitle = "Dernière mise à jour : 14 janvier 2026",
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
                ContactInfo = "Société : EZUPSOFT\nProduit : CatchCapture\nE-mail : ezupsoft@gmail.com\nSite web : https://ezupsoft.com\nDate d'entrée en vigueur : 14 janvier 2026",
                CloseLabel = "Fermer"
            };
        }

        private static PrivacyPolicyData GetDe()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture Datenschutzerklärung",
                Subtitle = "Zuletzt aktualisiert: 14. Januar 2026",
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
                ContactInfo = "Firma: EZUPSOFT\nProdukt: CatchCapture\nE-Mail: ezupsoft@gmail.com\nWebseite: https://ezupsoft.com\nDatum des Inkrafttretens: 14. Januar 2026",
                CloseLabel = "Schließen"
            };
        }

        private static PrivacyPolicyData GetAr()
        {
            return new PrivacyPolicyData
            {
                Title = "سياسة خصوصية CatchCapture",
                Subtitle = "آخر تحديث: 14 يناير 2026",
                Intro = "تلتزم شركة EZUPSOFT (\"نحن\") بالقوانين المتعلقة بحماية البيانات الشخصية ونسعى لحماية معلومات المستخدمين.",
                HighlightTitle = "【 هام 】",
                HighlightText = "لا يقوم CatchCapture بجمع أي معلومات شخصية.\nيعمل التطبيق محليًا على جهاز الكمبيوتر الخاص بك ولا يرسل أي بيانات إلى خوادم خارجية.",
                Sections = new List<(string, string)>
                {
                    ("1. جمع واستخدام المعلومات الشخصية",
                     "لا يقوم CatchCapture بجمع المعلومات التالية:\n• معرفات شخصية (الاسم، البريد الإلكتروني، الهاتف)\n• معلومات الموقع\n• معلومات حساب المستخدم\n• نشاط الشبكة أو الإنترنت"),
                    ("2. التخزين المحلي للبيانات",
                     "يتم تخزين إعدادات التطبيق والصور المُلتقطة فقط على جهاز الكمبيوتر الخاص بك ولا يتم إرسالها إلى خوادمنا أو أطراف ثالثة.\n\nمسار التخزين: %LocalAppData%\\CatchCapture"),
                    ("3. المشاركة مع أطراف ثالثة والمعالجة",
                     "بما أنه لا يتم جمع معلومات شخصية، لا توجد مشاركة أو تكليف مع أطراف ثالثة."),
                    ("4. حقوق المستخدم",
                     "يمكنك حذف التطبيق في أي وقت لإزالة جميع البيانات المخزنة محليًا."),
                    ("5. إجراءات الأمان",
                     "تتم معالجة جميع البيانات فقط على جهاز الكمبيوتر الخاص بك ومحمية بسياسات أمان Windows.")
                },
                ContactTitle = "الاتصال",
                ContactInfo = "الشركة: EZUPSOFT\nالمنتج: CatchCapture\nالبريد الإلكتروني: ezupsoft@gmail.com\nالموقع: https://ezupsoft.com\nتاريخ السريان: 14 يناير 2026",
                CloseLabel = "إغلاق"
            };
        }

        private static PrivacyPolicyData GetId()
        {
            return new PrivacyPolicyData
            {
                Title = "Kebijakan Privasi CatchCapture",
                Subtitle = "Terakhir diperbarui: 14 Januari 2026",
                Intro = "EZUPSOFT (\"kami\") mematuhi hukum privasi yang berlaku dan berkomitmen melindungi data pribadi pengguna.",
                HighlightTitle = "【 PENTING 】",
                HighlightText = "CatchCapture tidak mengumpulkan informasi pribadi apa pun.\nAplikasi berjalan secara lokal di komputer Anda dan tidak mengirimkan data apa pun ke server eksternal.",
                Sections = new List<(string, string)>
                {
                    ("1. Pengumpulan dan Penggunaan Informasi Pribadi",
                     "CatchCapture tidak mengumpulkan informasi berikut:\n• Pengenal pribadi (nama, email, telepon)\n• Informasi lokasi\n• Informasi akun pengguna\n• Aktivitas jaringan atau internet"),
                    ("2. Penyimpanan Data Lokal",
                     "Pengaturan aplikasi dan gambar yang ditangkap hanya disimpan di komputer Anda dan tidak dikirim ke server kami atau pihak ketiga.\n\nLokasi penyimpanan: %LocalAppData%\\CatchCapture"),
                    ("3. Berbagi dan Pemrosesan dengan Pihak Ketiga",
                     "Karena tidak ada informasi pribadi yang dikumpulkan, tidak ada berbagi atau outsourcing ke pihak ketiga."),
                    ("4. Hak Pengguna",
                     "Anda dapat menghapus aplikasi kapan saja untuk menghapus semua data yang disimpan secara lokal."),
                    ("5. Langkah Keamanan",
                     "Semua data hanya diproses di komputer Anda dan dilindungi oleh kebijakan keamanan Windows.")
                },
                ContactTitle = "Kontak",
                ContactInfo = "Perusahaan: EZUPSOFT\nProduk: CatchCapture\nEmail: ezupsoft@gmail.com\nSitus web: https://ezupsoft.com\nTanggal berlaku: 14 Januari 2026",
                CloseLabel = "Tutup"
            };
        }

        private static PrivacyPolicyData GetIt()
        {
            return new PrivacyPolicyData
            {
                Title = "Informativa sulla Privacy di CatchCapture",
                Subtitle = "Ultimo aggiornamento: 14 gennaio 2026",
                Intro = "EZUPSOFT (\"noi\") rispetta le leggi sulla privacy applicabili e si impegna a proteggere i dati personali degli utenti.",
                HighlightTitle = "【 IMPORTANTE 】",
                HighlightText = "CatchCapture non raccoglie alcuna informazione personale.\nL'app funziona localmente sul tuo computer e non invia dati personali a server esterni.",
                Sections = new List<(string, string)>
                {
                    ("1. Raccolta e utilizzo delle informazioni personali",
                     "CatchCapture non raccoglie le seguenti informazioni:\n• Identificativi personali (nome, email, telefono)\n• Informazioni sulla posizione\n• Informazioni sull'account utente\n• Attività di rete o internet"),
                    ("2. Archiviazione locale dei dati",
                     "Le impostazioni dell'app e le immagini catturate sono archiviate solo sul tuo computer e non trasmesse ai nostri server o a terze parti.\n\nPercorso di archiviazione: %LocalAppData%\\CatchCapture"),
                    ("3. Condivisione e elaborazione con terze parti",
                     "Poiché non vengono raccolte informazioni personali, non vi è condivisione né esternalizzazione a terze parti."),
                    ("4. Diritti dell'utente",
                     "Puoi eliminare l'app in qualsiasi momento per rimuovere tutti i dati archiviati localmente."),
                    ("5. Misure di sicurezza",
                     "Tutti i dati vengono elaborati solo sul tuo computer e protetti dalle policy di sicurezza di Windows.")
                },
                ContactTitle = "Contatti",
                ContactInfo = "Azienda: EZUPSOFT\nProdotto: CatchCapture\nEmail: ezupsoft@gmail.com\nSito web: https://ezupsoft.com\nData di entrata in vigore: 14 gennaio 2026",
                CloseLabel = "Chiudi"
            };
        }

        private static PrivacyPolicyData GetPt()
        {
            return new PrivacyPolicyData
            {
                Title = "Política de Privacidade do CatchCapture",
                Subtitle = "Última atualização: 14 de janeiro de 2026",
                Intro = "A EZUPSOFT (\"nós\") cumpre as leis de privacidade aplicáveis e está comprometida em proteger os dados pessoais dos usuários.",
                HighlightTitle = "【 IMPORTANTE 】",
                HighlightText = "O CatchCapture não coleta nenhuma informação pessoal.\nO aplicativo roda localmente no seu computador e não envia dados para servidores externos.",
                Sections = new List<(string, string)>
                {
                    ("1. Coleta e Uso de Informações Pessoais",
                     "O CatchCapture não coleta as seguintes informações:\n• Identificadores pessoais (nome, e-mail, telefone)\n• Informações de localização\n• Informações de conta do usuário\n• Atividade de rede ou internet"),
                    ("2. Armazenamento Local de Dados",
                     "As configurações do aplicativo e as imagens capturadas são armazenadas apenas no seu computador e não são transmitidas para nossos servidores ou terceiros.\n\nCaminho de armazenamento: %LocalAppData%\\CatchCapture"),
                    ("3. Compartilhamento e Processamento por Terceiros",
                     "Como nenhuma informação pessoal é coletada, não há compartilhamento nem terceirização para terceiros."),
                    ("4. Direitos do Usuário",
                     "Você pode excluir o aplicativo a qualquer momento para remover todos os dados armazenados localmente."),
                    ("5. Medidas de Segurança",
                     "Todos os dados são processados apenas no seu computador e protegidos pelas políticas de segurança do Windows.")
                },
                ContactTitle = "Contato",
                ContactInfo = "Empresa: EZUPSOFT\nProduto: CatchCapture\nE-mail: ezupsoft@gmail.com\nSite: https://ezupsoft.com\nData de vigência: 14 de janeiro de 2026",
                CloseLabel = "Fechar"
            };
        }

        private static PrivacyPolicyData GetRu()
        {
            return new PrivacyPolicyData
            {
                Title = "Политика конфиденциальности CatchCapture",
                Subtitle = "Последнее обновление: 14 января 2026 г.",
                Intro = "EZUPSOFT («мы») соблюдает применимые законы о конфиденциальности и стремится защищать персональные данные пользователей.",
                HighlightTitle = "【 ВАЖНО 】",
                HighlightText = "CatchCapture не собирает никакой персональной информации.\nПриложение работает локально на вашем компьютере и не отправляет данные на внешние серверы.",
                Sections = new List<(string, string)>
                {
                    ("1. Сбор и использование персональных данных",
                     "CatchCapture не собирает следующие данные:\n• Персональные идентификаторы (имя, email, телефон)\n• Информацию о местоположении\n• Данные учетной записи пользователя\n• Сетевую или интернет-активность"),
                    ("2. Локальное хранение данных",
                     "Настройки приложения и захваченные изображения хранятся только на вашем компьютере и не передаются на наши серверы или третьим лицам.\n\nПуть хранения: %LocalAppData%\\CatchCapture"),
                    ("3. Передача и обработка третьими лицами",
                     "Поскольку персональные данные не собираются, передача или обработка третьими лицами отсутствует."),
                    ("4. Права пользователя",
                     "Вы можете удалить приложение в любое время, чтобы удалить все локально сохраненные данные."),
                    ("5. Меры безопасности",
                     "Все данные обрабатываются только на вашем компьютере и защищены политиками безопасности Windows.")
                },
                ContactTitle = "Контакты",
                ContactInfo = "Компания: EZUPSOFT\nПродукт: CatchCapture\nEmail: ezupsoft@gmail.com\nСайт: https://ezupsoft.com\nДата вступления в силу: 14 января 2026 г.",
                CloseLabel = "Закрыть"
            };
        }

        private static PrivacyPolicyData GetTh()
        {
            return new PrivacyPolicyData
            {
                Title = "นโยบายความเป็นส่วนตัวของ CatchCapture",
                Subtitle = "อัปเดตล่าสุด: 14 มกราคม 2569",
                Intro = "EZUPSOFT (\"เรา\") ปฏิบัติตามกฎหมายความเป็นส่วนตัวที่เกี่ยวข้องและมุ่งมั่นปกป้องข้อมูลส่วนบุคคลของผู้ใช้",
                HighlightTitle = "【 สำคัญ 】",
                HighlightText = "CatchCapture ไม่เก็บรวบรวมข้อมูลส่วนบุคคลใด ๆ\nแอปทำงานแบบโลคอลบนคอมพิวเตอร์ของคุณและไม่ส่งข้อมูลใด ๆ ไปยังเซิร์ฟเวอร์ภายนอก",
                Sections = new List<(string, string)>
                {
                    ("1. การเก็บรวบรวมและการใช้ข้อมูลส่วนบุคคล",
                     "CatchCapture ไม่เก็บรวบรวมข้อมูลต่อไปนี้:\n• ตัวระบุส่วนบุคคล (ชื่อ อีเมล โทรศัพท์)\n• ข้อมูลตำแหน่ง\n• ข้อมูลบัญชีผู้ใช้\n• กิจกรรมเครือข่ายหรืออินเทอร์เน็ต"),
                    ("2. การเก็บข้อมูลแบบโลคอล",
                     "การตั้งค่าแอปและภาพที่จับได้จะถูกเก็บไว้เฉพาะบนคอมพิวเตอร์ของคุณและไม่ถูกส่งไปยังเซิร์ฟเวอร์ของเรา หรือบุคคลที่สาม\n\nตำแหน่งเก็บข้อมูล: %LocalAppData%\\CatchCapture"),
                    ("3. การแบ่งปันและการประมวลผลกับบุคคลที่สาม",
                     "เนื่องจากไม่มีการเก็บข้อมูลส่วนบุคคล จึงไม่มี การแบ่งปันหรือว่าจ้างบุคคลที่สาม"),
                    ("4. สิทธิของผู้ใช้",
                     "คุณสามารถลบแอปได้ทุกเมื่อเพื่อลบข้อมูลที่เก็บไว้ในเครื่องทั้งหมด"),
                    ("5. มาตรการรักษาความปลอดภัย",
                     "ข้อมูลทั้งหมดถูกประมวลผลเฉพาะบนคอมพิวเตอร์ของคุณและได้รับการป้องกันตามนโยบายความปลอดภัยของ Windows")
                },
                ContactTitle = "ติดต่อ",
                ContactInfo = "บริษัท: EZUPSOFT\nผลิตภัณฑ์: CatchCapture\nอีเมล: ezupsoft@gmail.com\nเว็บไซต์: https://ezupsoft.com\nวันที่มีผลบังคับใช้: 14 มกราคม 2569",
                CloseLabel = "ปิด"
            };
        }

        private static PrivacyPolicyData GetTr()
        {
            return new PrivacyPolicyData
            {
                Title = "CatchCapture Gizlilik Politikası",
                Subtitle = "Son güncelleme: 14 Ocak 2026",
                Intro = "EZUPSOFT (\"biz\") geçerli gizlilik yasalarına uyar ve kullanıcıların kişisel verilerini korumaya kararlıdır.",
                HighlightTitle = "【 ÖNEMLİ 】",
                HighlightText = "CatchCapture hiçbir kişisel bilgi toplamaz.\nUygulama bilgisayarınızda yerel olarak çalışır ve hiçbir kişisel veriyi harici sunuculara göndermez.",
                Sections = new List<(string, string)>
                {
                    ("1. Kişisel Bilgilerin Toplanması ve Kullanımı",
                     "CatchCapture aşağıdaki bilgileri toplamaz:\n• Kişisel tanımlayıcılar (isim, e-posta, telefon)\n• Konum bilgileri\n• Kullanıcı hesabı bilgileri\n• Ağ veya internet etkinliği"),
                    ("2. Yerel Veri Depolama",
                     "Uygulama ayarları ve yakalanan görüntüler yalnızca bilgisayarınızda saklanır ve sunucularımıza veya üçüncü taraflara iletilmez.\n\nDepolama yolu: %LocalAppData%\\CatchCapture"),
                    ("3. Üçüncü Taraflarla Paylaşım ve İşleme",
                     "Kişisel bilgi toplanmadığı için üçüncü taraflarla paylaşım veya dış kaynak kullanımı yoktur."),
                    ("4. Kullanıcı Hakları",
                     "Uygulamayı istediğiniz zaman silerek yerel olarak saklanan tüm verileri kaldırabilirsiniz."),
                    ("5. Güvenlik Önlemleri",
                     "Tüm veriler yalnızca bilgisayarınızda işlenir ve Windows güvenlik politikalarıyla korunur.")
                },
                ContactTitle = "İletişim",
                ContactInfo = "Şirket: EZUPSOFT\nÜrün: CatchCapture\nE-posta: ezupsoft@gmail.com\nWeb sitesi: https://ezupsoft.com\nYürürlük tarihi: 14 Ocak 2026",
                CloseLabel = "Kapat"
            };
        }

        private static PrivacyPolicyData GetVi()
        {
            return new PrivacyPolicyData
            {
                Title = "Chính sách Bảo mật của CatchCapture",
                Subtitle = "Cập nhật lần cuối: 14 tháng 1 năm 2026",
                Intro = "EZUPSOFT (\"chúng tôi\") tuân thủ các luật bảo mật hiện hành và cam kết bảo vệ dữ liệu cá nhân của người dùng.",
                HighlightTitle = "【 QUAN TRỌNG 】",
                HighlightText = "CatchCapture không thu thập bất kỳ thông tin cá nhân nào.\nỨng dụng chạy cục bộ trên máy tính của bạn và không gửi dữ liệu đến máy chủ bên ngoài.",
                Sections = new List<(string, string)>
                {
                    ("1. Thu thập và Sử dụng Thông tin Cá nhân",
                     "CatchCapture không thu thập các thông tin sau:\n• Định danh cá nhân (tên, email, điện thoại)\n• Thông tin vị trí\n• Thông tin tài khoản người dùng\n• Hoạt động mạng hoặc internet"),
                    ("2. Lưu trữ Dữ liệu Cục bộ",
                     "Cài đặt ứng dụng và hình ảnh chụp được chỉ lưu trữ trên máy tính của bạn và không được gửi đến máy chủ của chúng tôi hoặc bên thứ ba.\n\nĐường dẫn lưu trữ: %LocalAppData%\\CatchCapture"),
                    ("3. Chia sẻ và Xử lý với Bên thứ ba",
                     "Vì không thu thập thông tin cá nhân nên không có việc chia sẻ hoặc ủy thác xử lý cho bên thứ ba."),
                    ("4. Quyền của Người dùng",
                     "Bạn có thể xóa ứng dụng bất kỳ lúc nào để xóa toàn bộ dữ liệu lưu trữ cục bộ."),
                    ("5. Biện pháp Bảo mật",
                     "Tất cả dữ liệu chỉ được xử lý trên máy tính của bạn và được bảo vệ bởi chính sách bảo mật của Windows.")
                },
                ContactTitle = "Liên hệ",
                ContactInfo = "Công ty: EZUPSOFT\nSản phẩm: CatchCapture\nEmail: ezupsoft@gmail.com\nTrang web: https://ezupsoft.com\nNgày hiệu lực: 14 tháng 1 năm 2026",
                CloseLabel = "Đóng"
            };
        }
    }
}