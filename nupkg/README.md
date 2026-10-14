# ローカル フィード（NuGet パッケージの置き場）

本フォルダは、本リポジトリのプロジェクトが**相互参照に使うパッケージ**の置き場です。

- **外部へは公開しません**（nuget.org へは push しない。 `buildScript/pack.ps1` は `dotnet pack` までを行う）。
- **パッケージはリポジトリへコミットします**（`.gitignore` は本フォルダを無視しない）。 これにより、クローン直後でも `dotnet build` / `dotnet test` が解決できます。
- 供給元は `buildScript/pack.ps1` です（依存順に `dotnet pack` し、各段の後に古いキャッシュを捨てる）。
- 取得元の設定はルートの `NuGet.config` が持ちます（`local` を最優先にする）。
- **`.nupkg` を手で置かないでください**（再現できなくなる）。 更新は `pack.ps1` で行います。
