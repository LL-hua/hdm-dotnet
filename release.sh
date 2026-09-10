#!/data/data/com.termux/files/usr/bin/bash
set -e

# ========== 配置 ==========
# 用法:
#   cd ~/hdm-dotnet
#   bash release.sh                    # 自动版本号 + 自动备注
#   bash release.sh "修复了导入崩溃问题"   # 自定义备注
REPO_DIR="$HOME/hdm-dotnet"     # 项目目录
FOLDER="publish-net"            # 要发布的文件夹
# ==========================

cd "$REPO_DIR"

# 自动版本号：日期时间，永不重复
VERSION="v$(date +%Y%m%d-%H%M)"

# 备注：有参数用参数，没有就自动生成
NOTES="${1:-hdm-dotnet $VERSION 编译产物}"

echo "🆕 本次版本号: $VERSION"
echo "📝 备注: $NOTES"
echo ""

# 检查 gh 登录
if ! gh auth status >/dev/null 2>&1; then
  echo "❌ 未登录 gh，请先运行: gh auth login"
  exit 1
fi

# ========== 1. 提交源码 ==========
echo "📥 提交源码改动..."
git add -A

if git diff --cached --quiet; then
  echo "ℹ️  没有源码改动，跳过提交"
else
  git commit -m "Release $VERSION: $NOTES"
  git push origin main
  echo "✅ 源码已提交并推送"
fi

# ========== 2. 打包编译产物 ==========
if [ ! -d "$FOLDER" ]; then
  echo "❌ 找不到文件夹: $FOLDER"
  exit 1
fi

ZIP="publish-net-${VERSION}.zip"
TAR="publish-net-${VERSION}.tar.gz"

rm -f "$ZIP" "$TAR"

echo "📦 打包 zip..."
zip -rq "$ZIP" "$FOLDER"

echo "📦 打包 tar.gz..."
tar -czf "$TAR" "$FOLDER"

# ========== 3. 创建 Release ==========
echo "🚀 创建 Release $VERSION..."
gh release create "$VERSION" "$ZIP" "$TAR" \
  --title "$VERSION" \
  --notes "$NOTES"

echo ""
echo "✅ 发布完成！"
echo "🔗 https://github.com/LL-hua/hdm-dotnet/releases/tag/$VERSION"