# Shos.ImageUploadSample

ASP.NET Core MVCを使用した画像アップロードのサンプルアプリケーションです。ファイル選択またはカメラ撮影により画像をアップロードし、データベースに保存できます。

## 概要

このプロジェクトは、ASP.NET Core 8.0で構築された画像アップロード機能のデモンストレーションです。Entity Framework Coreを使用してSQL Serverデータベースに画像を保存し、自動的にサムネイルを生成します。

## 主な機能

- **ファイルアップロード**: ローカルファイルから画像を選択してアップロード
- **カメラ撮影**: ブラウザのカメラAPIを使用してリアルタイムで写真を撮影してアップロード
- **サムネイル生成**: アップロードされた画像の自動サムネイル作成
- **画像一覧表示**: アップロードされた全画像のサムネイルと詳細表示
- **データベース保存**: Entity Framework Coreを使用したSQL Serverへの画像データ保存

## 技術スタック

- **フレームワーク**: ASP.NET Core 8.0 MVC
- **データベース**: SQL Server (LocalDB)
- **ORM**: Entity Framework Core 8.0
- **画像処理**: System.Drawing.Common 8.0
- **フロントエンド**: HTML5, JavaScript, jQuery, Bootstrap

## 必要な環境

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server LocalDB (Visual Studio 2022に付属)
- Windows OS (System.Drawing.Commonの使用により Windows 6.1以降が必要)
- 対応ブラウザ (カメラ機能にはWebRTC対応ブラウザが必要)

## インストール手順

### 1. リポジトリのクローン

```bash
git clone https://github.com/Fujiwo/Shos.ImageUploadSample.git
cd Shos.ImageUploadSample
```

### 2. プロジェクトのビルド

```bash
cd ImageUploadSample
dotnet restore
dotnet build
```

### 3. データベースのセットアップ

Entity Framework Coreマイグレーションを使用してデータベースを作成します。

```bash
# EF Coreツールのインストール (初回のみ)
dotnet tool install --global dotnet-ef

# マイグレーションの実行
dotnet ef database update
```

データベース接続文字列は `appsettings.json` で設定されています：

```json
"ConnectionStrings": {
  "ImageContext": "Server=(localdb)\\mssqllocaldb;Database=ImageUploadSample-ecb85b14-ebd2-4744-bec9-be8f021688b7;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

### 4. アプリケーションの起動

```bash
dotnet run
```

ブラウザで `https://localhost:5001` (または表示されたURLポート) にアクセスしてください。

## 使い方

### ファイルから画像をアップロード

1. アプリケーションのホームページにアクセス
2. 「Choose Image」ボタンをクリックして画像ファイルを選択
3. 説明文を入力 (オプション)
4. 「Upload」ボタンをクリック

### カメラで撮影してアップロード

1. 「Camera」ボタンをクリックしてカメラを起動
2. カメラへのアクセス許可を承認
3. 「Snap」ボタンで写真を撮影
4. 説明文を入力 (オプション)
5. 「Upload」ボタンをクリック

### 画像の表示

- アップロードされた画像はサムネイルとして上部に表示されます
- サムネイルをクリックすると、ページ下部のフルサイズ画像にジャンプします

## プロジェクト構成

```
ImageUploadSample/
├── Controllers/
│   └── ImagesController.cs    # 画像アップロードロジック
├── Models/
│   ├── ImageContext.cs         # Entity Framework DbContext
│   └── ErrorViewModel.cs
├── Views/
│   └── Images/
│       └── Upload.cshtml       # 画像アップロードUI
├── Migrations/                 # EF Core マイグレーション
├── appsettings.json           # 設定ファイル
└── Program.cs                 # アプリケーションエントリポイント
```

## 制限事項

- 最大ファイルサイズ: 2MB
- サポート対象OS: Windows 6.1以降 (System.Drawing.Common の要件)
- カメラ機能: HTTPS環境とWebRTC対応ブラウザが必要

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細は [LICENSE.txt](LICENSE.txt) ファイルを参照してください。

## 作者

Fujiwo

## 参考資料

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core/)
- [Entity Framework Core Documentation](https://docs.microsoft.com/ef/core/)
- [System.Drawing.Common](https://docs.microsoft.com/dotnet/api/system.drawing)