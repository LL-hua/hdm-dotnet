#!/data/data/com.termux/files/usr/bin/bash
set -e

# ========== 配置 ==========
# 运行 cd ~/hdm-dotnet
# 直接跑，版本号自动生成，备注自动
#   bash release.sh
# 带自定义备注
#   bash release.sh "修复了导入崩溃问题"
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

# ========== 1. 自动提交源码 ==========
echo "📥 提交源码改动..."

# 改成（排除压缩包）
git add -A
git reset -- '*.zip' '*.tar.gz' publish-net/ 2>/dev/null || true
if git diff --cached --quiet; then
  echo "ℹ️  没有源码改动，跳过提交"
else
  git commit -m "Release $VERSION: $NOTES"
  echo "✅ 源码已提交"
fi

echo "📤 推送源码到 GitHub..."
git push origin main

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