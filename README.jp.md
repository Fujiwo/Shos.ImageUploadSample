# Shos.ImageUploadSample

ASP.NET Core MVCを使用した画像アップロードのサンプルアプリケーションです。ファイル選択またはカメラ撮影により画像をアップロードし、データベースに保存できます。

## 概要

このプロジェクトは、ASP.NET Core 10.0で構築された画像アップロード機能のデモンストレーションです。Entity Framework Coreを使用してSQL Serverデータベースに画像を保存し、SkiaSharpで自動的にサムネイル（高さ100px）を生成します。

## 主な機能

- **ファイルアップロード**: ローカルファイルから画像を選択してアップロード
- **カメラ撮影**: ブラウザのカメラAPIを使用してリアルタイムで写真を撮影してアップロード
- **サムネイル生成**: アップロードされた画像の自動サムネイル作成
- **画像一覧表示**: アップロードされた画像のサムネイルと詳細表示（新しい順、1ページ24件のページング）
- **データベース保存**: Entity Framework Coreを使用したSQL Serverへの画像データ保存

## 技術スタック

- **フレームワーク**: ASP.NET Core 10.0 MVC
- **データベース**: SQL Server (LocalDB)
- **ORM**: Entity Framework Core 10.0
- **画像処理**: SkiaSharp (MIT ライセンス、クロスプラットフォーム)
- **テスト**: xUnit, Microsoft.AspNetCore.Mvc.Testing, SQLite (インメモリ)
- **フロントエンド**: HTML5, JavaScript, jQuery, Bootstrap

## 必要な環境

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server LocalDB (Visual Studio に付属、Windows のみ)
  - Linux / macOS では SQL Server (Docker コンテナなど) を用意し、接続文字列を変更してください
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

データベース接続文字列は `appsettings.json` の `ConnectionStrings:ImageContext` で設定されています。接続先やデータベース名はここで変更できます（既定のデータベース名は `ImageUploadSample-ecb85b14-...` のように GUID 付きです）：

```json
"ConnectionStrings": {
  "ImageContext": "Server=(localdb)\\mssqllocaldb;Database=ImageUploadSample-ecb85b14-ebd2-4744-bec9-be8f021688b7;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

`dotnet ef` も `Program.cs` の設定を使うため、`dotnet ef database update` も同じ接続先に対して実行されます。

エンティティ（`Models/ImageContext.cs` の `ImageRecord`）を変更した場合は、マイグレーションを追加してから適用します：

```bash
dotnet ef --version                       # ツールのバージョン確認
dotnet tool update --global dotnet-ef     # ツールの更新
dotnet ef migrations add <マイグレーション名>
dotnet ef database update
```

### 4. アプリケーションの起動

```bash
dotnet run
```

アプリケーションが起動したら、コンソールに表示されるURLをブラウザで開いてください（`launchSettings.json` の設定では `https://localhost:7020` または `http://localhost:5257`）。

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

- アップロードされた画像はサムネイルとして上部に表示されます（24件を超えるとページングされます）
- サムネイルをクリックすると、ページ下部のフルサイズ画像にジャンプします

## プロジェクト構成

```
ImageUploadSample/
├── Controllers/
│   └── ImagesController.cs    # アップロード、一覧、画像配信 (Thumbnail / Original)、エラーページ
├── Models/
│   ├── ImageContext.cs         # エンティティ ImageRecord と DbContext
│   ├── ImageListViewModel.cs   # 一覧表示用ビューモデル
│   └── ErrorViewModel.cs       # エラーハンドリング用ビューモデル
├── Services/
│   └── ImageProcessor.cs       # 画像の検証、寸法取得、サムネイル生成、data URL の解析
├── Views/
│   └── Images/
│       └── Upload.cshtml       # 画像アップロードUI
├── Migrations/                 # EF Core マイグレーション
├── appsettings.json           # 設定ファイル
└── Program.cs                 # アプリケーションエントリポイント
ImageUploadSample.Tests/        # 単体テストと結合テスト (xUnit)
```

## テスト

```bash
dotnet test
```

結合テストは `WebApplicationFactory` を使い、SQL Server の代わりにインメモリの SQLite で実行されるため、Windows 以外でも動作します。GitHub Actions (`.github/workflows/build.yml`) でもビルドとテストを実行しています。

## 制限事項

- 最大ファイルサイズ: 2MB（ファイル、カメラとも）
- 最大画像サイズ: 幅・高さともに 8000px
- 対応形式: PNG, JPEG, GIF, BMP, WebP（MIME 型はファイルの内容から判定します）
- 説明文: 最大 200 文字
- カメラ機能: HTTPS環境とWebRTC対応ブラウザが必要
- **認証・認可はありません**。誰でも画像をアップロードでき、アップロードされた画像はすべて誰にでも公開されます。学習・デモ用のサンプルとして使用し、そのまま公開環境で運用しないでください

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細は [LICENSE.txt](LICENSE.txt) ファイルを参照してください。

## 作者

Fujio Kojima

## 参考資料

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core/)
- [Entity Framework Core Documentation](https://docs.microsoft.com/ef/core/)
- [SkiaSharp](https://github.com/mono/SkiaSharp)
