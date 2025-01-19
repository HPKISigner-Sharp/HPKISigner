![platform](https://img.shields.io/static/v1?label=platform&message=win-64%20|%20mac-intel%20|%20mac-arm%20|%20linux&color=blue)
![Language: C#](https://img.shields.io/badge/Language-C%23-blue.svg)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

# HPKISigner

ES フォーマットの電子署名付き処方箋を作成するツール。

## 特徴
- 100% C#
  
## システム要件
- **HPKI card and its driver**
- **.NET Version**: [.NET 8.0 Runtime or heigher](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Dependencies**:
  - [Pkcs11Interop](https://github.com/Pkcs11Interop/Pkcs11Interop)
  - [BouncyCastle.Cryptography](https://www.bouncycastle.org/csharp/)

## サポートプラットフォーム
- Windows (x64)
- Linux (x64)
- macOS (x64 and arm64)

## オプション
### ランタイムなしバージョン (サイズ小)
  - ファイル名: HPKISigner.exe
  - サイズ: ~6 MB
  - 要件: 事前に [.NET 8.0 Runtime or heigher](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) インストール必須

### ランタイム入りバージョン (サイズ大)
  - ファイル名: HPKISigner-Large.exe
  - サイズ: ~70 MB
  - 要件: 全ライブラリ、.NET runtime を内包、追加インストール不要。

## 使用方法
1. https://github.com/HPKISigner-Sharp/HPKISigner/releases から、HPKISigner.zip をダウンロードして解凍する。
2. コマンドプロンプトを開いて
```cmd
HPKISigner.exe InputCSVFilePath OutputXMLPath PIN
```
- **InputCSVFilePath**: 処理するCSVファイルのパス。
- **OutputXMLPath**: 署名されたXMLを保存するパス。
- **PIN**: HPKIカードのPIN。

☞ HPKIカードを接続し、3つの引数を指定しないと、**動きません。**

### 使用例:
```cmd
HPKISigner.exe "C:\電子処方箋\input.csv" "C:\電子処方箋\output.xml" 123456
```

## 作成者
- **MentorSystems**  

## ライセンス
HPKISigner is licensed under the [MIT license](https://en.wikipedia.org/wiki/MIT_License).

