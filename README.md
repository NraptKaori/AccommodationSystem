# 宿泊税徴収管理・領収書発行システム

## 動作環境
- Windows 11
- .NET Framework 4.8 以上
- インターネット接続（Stripe決済・メール送信時）

## セットアップ手順

### 1. Visual Studio でビルド
```
Visual Studio 2022 でソリューションを開き「ビルド」→「ソリューションのビルド」
```

必要なNuGetパッケージは自動的に復元されます：
- System.Data.SQLite（ローカルDB）
- Stripe.net（決済処理）
- MailKit（メール送信）
- PdfSharp（PDF生成）
- ClosedXML（Excel出力）
- BCrypt.Net-Next（パスワードハッシュ）

### 2. 初回起動
アプリ起動後、管理画面から以下を設定してください：

| 設定項目 | 内容 |
|---------|------|
| 施設名・住所 | 領収書に表示される情報 |
| Stripe APIキー | Stripe管理画面から取得したSecretKey（sk_live_...） |
| SMTP設定 | Gmail等のメール送信設定 |
| 宿泊税単価 | デフォルト200円（変更可） |

### 3. 初期パスワード
管理画面パスワード: `admin1234`（初回ログイン後に必ず変更してください）

## データ保存場所
```
C:\ProgramData\AccommodationSystem\accommodation.db
```

## CSVインポート形式
Booking.comなどからエクスポートしたCSV（UTF-8）

必須列（英語または日本語ヘッダー）：
- `Reservation number` / `予約番号`
- `Arrival` / `チェックイン日`
- `Departure` / `チェックアウト日`
- `Guest name` / `宿泊者名`
- `Persons` / `宿泊人数`
- `Room nights` / `宿泊泊数`

## 運用フロー
1. 予約サイトからCSVをダウンロード
2. 管理画面 > 宿泊者一覧 > 「CSV取込」
3. 宿泊者がチェックイン時に端末で予約番号または氏名を検索
4. カード決済を完了（Stripe処理）
5. 必要に応じてメールで領収書を受領
6. 月末に管理画面 > 月次集計 > Excelで出力

## Stripe テスト用カード番号
テスト環境（sk_test_...）では以下のカードが使えます：
- 成功: `4242 4242 4242 4242`
- 有効期限: 任意の未来の日付
- CVC: 任意の3桁

## セキュリティ
- パスワードはbcryptハッシュで保存
- Stripe APIキーは暗号化保存（DPAPI）
- ログイン失敗5回でロック
- 全決済・管理操作をaudit_logに記録

## ファイル構成
```
AccommodationSystem/
├── Models/          # データモデル
├── Data/            # SQLiteアクセス
├── Services/        # Stripe・メール・PDF・Excel・CSV
├── Views/           # WPF画面
│   ├── MainWindow         # メイン画面（ヘッダー+Frame）
│   ├── CheckinPage        # 宿泊者検索・一覧
│   ├── PaymentWindow      # 決済画面
│   ├── ReceiptEmailWindow # 領収書メール送信
│   ├── AdminLoginWindow   # 管理ログイン
│   └── AdminWindow        # 管理画面（一覧/集計/設定）
└── Assets/          # スタイル定義
```
