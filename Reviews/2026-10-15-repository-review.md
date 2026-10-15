# Shos.ImageUploadSample リポジトリ全体レビュー

- レビュー日: 2026-10-15
- 対象コミット: `a954a44`（.NET 10.0 / EF Core 10.0.13 への更新）
- 対象範囲: Git で追跡されているすべてのファイル（`wwwroot/lib` 配下のサードパーティライブラリは対象外）
- 前提: 学習・デモ用のサンプルアプリケーションです。本番運用を前提とした指摘は、その旨を明記しています。

## 総評

構成は小さく、エントリポイント（`Program.cs`）→ コントローラー（`ImagesController`）→ EF Core（`ImageContext`）→ ビュー（`Upload.cshtml`）の流れがひと目で分かります。サンプルとしての見通しは良好です。一方で、次の点に問題があります。

1. **サムネイルが縮小されていない**（機能バグ）
2. **リソースの未解放と入力検証の不備**: 不正な入力が 400 ではなく 500 になります。
3. **接続文字列が二重に定義され、`appsettings.json` の設定が効かない**
4. **一覧表示で全画像のバイナリを毎回読み込み、HTML に埋め込んでいる**: 画像が増えると破綻します。
5. **CSRF 対策が無効**: トークンを出力していますが、検証していません。

## 重大度の凡例

| 重大度 | 意味 |
|---|---|
| 🔴 High | 機能が仕様どおりに動かない、または明確なセキュリティ・安定性リスク |
| 🟠 Medium | 条件次第で不具合や性能劣化を起こす、または設定・ドキュメントの誤り |
| 🟢 Low | 保守性・可読性・細かな改善 |

---

## 1. 正確性（バグ）

### 🔴 1-1. サムネイルが元画像と同じサイズで生成されている

`ImageUploadSample/Controllers/ImagesController.cs:120`, `:131`

```csharp
var (thumbnailWidth, thumbnailHeight) = (thumbnailSize * width / height, thumbnailSize);
...
var thumbnail = image.GetThumbnailImage(width, height, () => false, IntPtr.Zero);
```

内側の `CreateThumbnail` には元画像の `width` と `height` がそのまま渡されています。そのため、計算した `thumbnailWidth` と `thumbnailHeight` は使われていません。

- 結果として、**原寸の PNG** が `ThumbnailData` に保存されます。
- 元画像が JPEG の場合、PNG に再エンコードされた「サムネイル」のほうが元画像より大きくなることもあります。
- 一覧表示では `width`/`height` 属性で 100px 程度に縮めて表示されるため、見た目では気づきにくいバグです。

**修正案**: 内側の関数に `thumbnailWidth` と `thumbnailHeight` を渡します。

### 🟠 1-2. ファイルサイズ超過時に誤ったエラーメッセージを返し、後続のチェックは到達不能

`ImagesController.cs:30-34`

```csharp
if (file == null || file.Length == 0 || file.Length > maximumFileSize)
    return BadRequest("No file selected.");

if (file.Length > maximumFileSize)      // 到達しない
    return BadRequest("Too large file.");
```

最初の条件に `file.Length > maximumFileSize` が含まれているため、2MB を超えるファイルでも「No file selected.」が返ります。最初の条件から `file.Length > maximumFileSize` を取り除いてください。

### 🟠 1-3. 不正な画像は 400 ではなく 500 になり、`width == 0` のチェックは機能しない

`ImagesController.cs:39-41`, `:111`

`new System.Drawing.Bitmap(stream)` は、画像として解釈できないデータに対して `ArgumentException` を投げます。0×0 の画像を返すことはありません。そのため次のことが起きます。

- `if (width == 0 || height == 0) return BadRequest("Invalid image.")` は事実上のデッドコードです。
- 画像以外のファイルをアップロードすると、未処理例外として 500 になります。

**修正案**: `GetImageDimensions` 内で `ArgumentException` を捕捉し、失敗時は `null` を返して 400 にします。

### 🟠 1-4. カメラ撮影時の説明文が保存されない

`ImagesController.cs:65`

```csharp
await Save(contentType, "", width, height, imageData);
```

引数の `description` が使われていません。クライアント側（`Upload.cshtml:142-145`）は `description` を送信しているため、入力欄に何を書いても保存されません。`""` を `description` に置き換えてください。

### 🟠 1-5. Base64 の解析が脆く、形式不正は 500 になる

`ImagesController.cs:70-78`, `:61`

- `SplitImageBase64` は `;` で分割できるかしか確認していません。`data:image/png,xxx`（`;base64` なし）や任意の文字列を受け付けます。その後 `Convert.FromBase64String` が `FormatException` を投げ、500 になります。
- `Replace` は文字列中のすべての一致箇所を置換します。先頭のプレフィックスだけを除去する意図であれば、`StartsWith` で確認してから `Substring` で切り出すべきです。
- 1-3 と同様に、`GetImageDimensions` の例外も捕捉されていません。

**修正案**: `^data:(image/(png|jpeg|gif|bmp));base64,` を正規表現などで厳密に検証します。デコードには `Convert.TryFromBase64String` を使います。

### 🟠 1-6. 存在しない `HomeController` を参照している

- `Program.cs:17`: `app.UseExceptionHandler("/Home/Error")`
- `Views/Shared/_Layout.cshtml:20`: `asp-controller="Home" asp-action="Privacy"`

`HomeController` が存在しません。そのため、本番環境で例外が起きると、エラーページの表示自体が 404 で失敗します。フッターの Privacy リンクも壊れています（リンク先が生成されず、空の href になります）。

**修正案**: エラーハンドラーを `ImagesController` などに移すか、最小限の `HomeController`（`Error` アクション）を追加します。Privacy リンクは削除します。

### 🟢 1-7. 画像の表示順が不定

`ImagesController.cs:20`: `context.Images.ToListAsync()` に `OrderBy` がありません。SQL Server は順序を保証しないため、`OrderBy(i => i.Id)` または `OrderByDescending(i => i.CreatedAt)` を明示してください。

---

## 2. セキュリティ・堅牢性

### 🔴 2-1. 解凍爆弾（decompression bomb）によるメモリ枯渇

`ImagesController.cs:111`, `:130`

チェックしているのはファイルサイズ（2MB）だけで、画像の**ピクセル数**は確認していません。たとえば 2MB 以下で 30000×30000 ピクセルの PNG を作ることは容易です。このような画像を `Bitmap` が展開すると、数 GB のメモリを確保しようとします。さらに `CreateThumbnail` は同じ画像をもう一度展開します（1-1 のバグにより原寸のまま）。

**修正案**:
- デコードする前にヘッダーから寸法を読み取り、上限（例: 8000×8000）を超える画像は拒否します。
- 下記 4-1 のライブラリ移行に合わせて、ライブラリ側の上限設定を使います。

### 🟠 2-2. CSRF トークンを検証していない

`ImagesController.cs:24-25`, `:49-50`

`<form method="post">` タグヘルパーは、Antiforgery トークンを自動でフォームに出力します。しかし、アクションに `[ValidateAntiForgeryToken]` がなく、グローバルな `AutoValidateAntiforgeryTokenAttribute` も登録されていないため、トークンは検証されていません。悪意のあるサイトから、閲覧者を経由して任意の画像を投稿できます。

**修正案**:
- `Program.cs` で `AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))` を設定します。
- カメラ用の `$.ajax` 呼び出しで、`RequestVerificationToken` ヘッダーにトークンを付けて送ります。

### 🟠 2-3. クライアントが申告した Content-Type をそのまま保存している

`ImagesController.cs:44`（`file.ContentType`）, `:75`

保存する `MimeType` はクライアントが申告した値で、`data:` URI の生成（`Upload.cshtml:8`）に使われます。Razor が属性値をエンコードし、`<img>` 内の `data:` URI ではスクリプトが実行されないため、直ちに XSS にはなりません。ただし、保存内容と MIME 型が一致しないデータが残ります。

**修正案**: デコード結果の `Bitmap.RawFormat`（`ImageFormat.Png`/`Jpeg` など）から MIME 型を決め、許可リストにない形式は拒否します。

### 🟠 2-4. カメラ経由のアップロードにサイズ上限がない

`UploadCameraImage` には、ファイル経由の 2MB チェックに相当する制限がありません。リクエストサイズは、フォーム値の既定上限（`FormOptions.ValueLengthLimit` = 4MB）と Kestrel の既定上限（30MB）だけに依存しています。

**修正案**: デコード後のバイト長を同じ定数で検証し、アクションに `[RequestSizeLimit]` を付けます。

### 🟢 2-5. 説明文の長さに上限がない

`Description` は `nvarchar(max)` で、長さの検証がありません。`[MaxLength]` を付け、入力欄に `maxlength` を指定することを推奨します。

### 🟢 2-6. 認証・認可がない（本番を想定する場合）

誰でもアップロードでき、全画像が誰にでも公開されます。サンプルとしては問題ありませんが、README の「制限事項」に明記しておくと誤用を防げます。

---

## 3. リソース管理・パフォーマンス

### 🔴 3-1. `Bitmap`/`Image` を Dispose していない（GDI+ ハンドルのリーク）

`ImagesController.cs:111`, `:130-132`

```csharp
var image = new Drawing.Bitmap(stream);            // Dispose されない
...
var image     = new Drawing.Bitmap(stream);        // Dispose されない
var thumbnail = image.GetThumbnailImage(...);      // 呼び出し元でも Dispose されない
```

`System.Drawing` のオブジェクトは、アンマネージドな GDI+ リソースを保持しています。GC の回収を待つ間にハンドルやネイティブメモリを消費するため、アップロードが続くと `OutOfMemoryException` や「A generic error occurred in GDI+」の原因になります。いずれも `using var` を付けてください。

### 🟠 3-2. 一覧表示で全画像のバイナリを読み込み、HTML に埋め込んでいる

- `ImagesController.cs:20`: `context.Images.ToListAsync()` が、`Data`（原寸）と `ThumbnailData` をすべて読み込みます。
- `Upload.cshtml:8`, `:11`: 全画像を Base64（約 1.33 倍）に変換し、1 枚の HTML に埋め込みます。

画像数 N に比例して、DB 転送量、サーバーメモリ、HTML サイズが増えます。キャッシュも効きません。2MB の画像が 50 枚あれば、HTML は 130MB を超えます。

**修正案**:
- `GET /Images/Thumbnail/{id}` と `GET /Images/Original/{id}` のような `FileContentResult` を返すアクションを追加し、`<img src>` からはそれを参照します。
- 一覧は `Select` で Id、寸法、説明だけを射影します。
- 件数が多い場合はページングします。

### 🟢 3-3. 同じ画像を 2 回デコードしている

`GetImageDimensions` と `CreateThumbnail` が、同じバイト列からそれぞれ `Bitmap` を生成しています。1 回のデコードで寸法の取得とサムネイル生成を両方行う構成にすると、CPU とメモリを節約できます。

---

## 4. プラットフォーム・依存関係

### 🟠 4-1. `System.Drawing.Common` は Windows 専用で、サーバー用途に非推奨

`ImageUploadSample.csproj:16`

- .NET 6 以降、`System.Drawing.Common` は Windows 以外ではサポートされません。コード側でも `[SupportedOSPlatform("windows")]` で抑制しています。Linux コンテナや Azure App Service (Linux) では動きません。
- Microsoft は、サーバーサイド（ASP.NET Core）での `System.Drawing` の使用を推奨していません。
- 信頼できない画像を GDI+ でパースすること自体が、攻撃対象領域になります。

**修正案**: `SixLabors.ImageSharp`、`SkiaSharp`、`Magick.NET` などのクロスプラットフォームなライブラリへ移行します。ImageSharp なら `Image.Identify` で寸法だけを事前に取得できるため、2-1 の対策にもなります。

### 🟢 4-2. `System.Security.Cryptography.Pkcs` を直接参照している理由が不明

`ImageUploadSample.csproj:17`

`a954a44` で .NET 10 / EF Core 10.0.13 に更新され、サポート期限の問題は解消しました。同じコミットで `System.Security.Cryptography.Pkcs` が直接参照に追加されています。ただし、コード中に使用箇所はありません。

脆弱な推移的依存のバージョンを引き上げる目的であれば、csproj にその理由をコメントで残してください。理由が分からないままだと、後で「未使用パッケージ」として削除されるおそれがあります。不要になったかどうかは、定期的に `dotnet list package --vulnerable --include-transitive` で確認してください。

---

## 5. 設定・構成

### 🟠 5-1. 接続文字列が二重に定義され、`appsettings.json` が効かない

- `Program.cs:8-10`: `AddDbContext` で `appsettings.json` の `ConnectionStrings:ImageContext` を設定しています。
- `Models/ImageContext.cs:33-35`, `:46-52`: `OnConfiguring` が、ハードコードされた接続文字列で無条件に `UseSqlServer` を呼びます。

`OnConfiguring` は DI 経由で生成された場合にも呼ばれ、後から設定を上書きします。そのため、`appsettings.json` を書き換えても接続先は変わりません。README の「必要に応じてデータベース名を変更できます」という説明と矛盾しています。

**修正案**:

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    if (!optionsBuilder.IsConfigured)
        optionsBuilder.UseSqlServer(connectionString);
}
```

パラメーターなしのコンストラクターとハードコードされた接続文字列は、`dotnet ef` のためだけに残っていると思われます。代わりに `IDesignTimeDbContextFactory<ImageContext>` を実装すれば、両方とも削除できます。

### 🟢 5-2. コメントアウトされた旧接続文字列が残っている

`appsettings.json:10` と `ImageContext.cs:34` に、SQL Server Express 用の旧設定がコメントとして残っています。Git に履歴があるので削除して構いません。

### 🟢 5-3. `Debug.WriteLine` へのログ出力が DbContext に直書きされている

`ImageContext.cs:49-52`: ASP.NET Core のロギング（`ILogger`）とは別経路になっています。不要であれば削除し、EF Core のログレベルは `appsettings.Development.json` の `"Microsoft.EntityFrameworkCore.Database.Command"` で制御するほうが一般的です。

### 🟢 5-4. `ImageUploadSample.csproj.user` がコミットされている

`.csproj.user` は IDE の個人設定ファイルです。`.gitignore` に追加して追跡を外すことを推奨します。

---

## 6. フロントエンド（`Views/Images/Upload.cshtml`）

### 🟠 6-1. カメラアップロードにエラー処理がなく、UI が先に閉じる

`Upload.cshtml:138-155`

- `$.ajax` に `.fail()` がありません。失敗してもユーザーには何も表示されません。
- リクエスト完了を待たずにプレビューと入力欄を非表示にしているため、失敗すると撮影した画像も失われます。
- サーバーは `RedirectToAction` を返し、XHR はリダイレクト先の HTML 全体を受け取ります。その後 `location.reload()` を呼ぶので、一覧 HTML を 2 回取得しています。Ajax 用のアクションは `Ok()` や `NoContent()` を返すほうが適切です。

### 🟢 6-2. グローバル変数 `canvas` に暗黙的に依存している

`Upload.cshtml:114`: `snap()` 内の `canvas.getContext('2d')` は、`id="canvas"` 要素がグローバル変数として公開されるブラウザーの仕様に依存しています。`document.querySelector('#canvas')` で明示的に取得してください。

### 🟢 6-3. `<video />` と `<canvas />` の自己終了タグ

`Upload.cshtml:64`, `:67`: HTML ではこの 2 つの要素は自己終了できません。現状はパーサーの補正で動いていますが、`<video ...></video>` と明示的に閉じるべきです。

### 🟢 6-4. カメラストリームが二重に起動する

「Camera」を連続で押すと `getUserMedia` が再度呼ばれますが、既存のストリームは停止されません。カメラのインジケーターが点いたままになる場合があります。

### 🟢 6-5. 入力欄にラベルとプレースホルダーがない

説明文の入力欄（`Upload.cshtml:48`, `:69`）にはラベルもプレースホルダーもなく、用途が分かりません。アクセシビリティの観点からも `<label>` を付けてください。

---

## 7. コード品質

| 箇所 | 指摘 |
|---|---|
| `ImagesController.cs:14` | `context` フィールドは `readonly` にできます。 |
| `ImagesController.cs:9` | `using System.IO;` は ImplicitUsings により不要です。 |
| `ImagesController.cs:56`, `:92` | 綴り: `splitedImageBase64` → `splitImageBase64`、`thumnailWidth` → `thumbnailWidth` |
| `ImagesController.cs:109` | `GetImageDimensions` だけが非 static です（他のヘルパーは static）。 |
| `ImagesController.cs:81-87` | `Save` は `Image` を生成して保存するだけなので、`ToImage` との分割は過剰です。 |
| `Models/ImageContext.cs:1-6` | EF コマンドのメモがコメントとして残っています。README に移すことを推奨します。 |
| `Models/ImageContext.cs` | エンティティ `Image` は `System.Drawing.Image` と名前が衝突しやすいです（コントローラーではエイリアスで回避しています）。`ImageRecord` などへの改名を検討してください。 |
| `Models/ErrorViewModel.cs` | ほかのファイルはファイルスコープ namespace ですが、ここだけブロックスコープです。 |
| `Views/Shared/_Layout.cshtml:20` | 著作権表記が `© 2023` のままです。 |

---

## 8. テスト・CI・ドキュメント

### 🟠 8-1. テストがない

テストプロジェクトがありません。最低限、次のテストを推奨します。

- `SplitImageBase64` と、サムネイルの寸法計算の単体テスト（1-1 と 1-5 の回帰防止）
- `WebApplicationFactory` と EF Core の InMemory / SQLite を使った、アップロードの結合テスト

### 🟢 8-2. CI がない

`.github/workflows` はありますが空です。`dotnet build`（と、追加すればテスト）を実行するワークフローを置くと、PR ごとの破損を検知できます。ただし 4-1 のとおり、現状は `windows-latest` ランナーが必要です。

### 🟢 8-3. README と実装の不一致

| README の記述 | 実際 |
|---|---|
| 起動 URL は `https://localhost:5001` または `http://localhost:5000` | `launchSettings.json` では `https://localhost:7020` / `http://localhost:5257` |
| ASP.NET Core 8.0 / EF Core 8.0 / System.Drawing.Common 8.0（概要、技術スタック、必要な環境） | csproj は .NET 10.0 / 10.0.13（`a954a44` で更新）。`CLAUDE.md` にも「EF Core 8」の記述が残っている |
| DB 名は `ImageUploadSample` | 実際は `ImageUploadSample-ecb85b14-...`（GUID 付き） |
| `appsettings.json` で接続先を変更できる | 5-1 のとおり変更できない |
| 画像をカメラで撮影し、説明文を入力してアップロード | 1-4 のとおり説明文は保存されない |

---

## 推奨対応の優先順位

| 優先度 | 項目 | 作業量 |
|---|---|---|
| 1 | 1-1 サムネイルの縮小バグ | 小（引数 2 つの修正） |
| 2 | 3-1 `Bitmap` の Dispose | 小 |
| 3 | 1-2 / 1-3 / 1-4 / 1-5 入力検証とエラーハンドリング | 小〜中 |
| 4 | 5-1 接続文字列の二重定義 | 小 |
| 5 | 2-2 CSRF 検証の有効化 | 小 |
| 6 | 1-6 `/Home/Error` の参照先の修正 | 小 |
| 7 | 2-1 / 2-4 画像寸法とリクエストサイズの上限 | 中 |
| 8 | 3-2 画像配信エンドポイントの分離 | 中 |
| 9 | 4-1 ImageSharp への移行 | 中〜大 |
| 10 | 8-1 / 8-2 テストと CI の追加 | 中 |
