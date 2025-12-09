<template>
  <div class="image-gallery-container">
    <div class="header">
      <h2>图片展示</h2>
      <el-button type="primary" @click="showUploadDialog = true">上传图片</el-button>
      <el-button @click="$router.push('/logout')">退出登录</el-button>
    </div>
    
    <!-- 图片列表 -->
    <div class="image-list">
      <el-card v-for="image in images" :key="image.id" class="image-card">
        <template #header>
          <div class="card-header">
            <span>{{ image.filename }}</span>
            <el-button type="danger" size="small" @click="handleDelete(image.id)">删除</el-button>
          </div>
        </template>
        <div class="image-content">
          <img :src="getImageUrl(image.filepath)" :alt="image.filename" @click="previewImage(image)">
        </div>
        <div class="image-info">
          <span>上传时间: {{ formatDate(image.created_at) }}</span>
        </div>
      </el-card>
    </div>
    
    <!-- 上传图片对话框 -->
    <el-dialog v-model="showUploadDialog" title="上传图片" width="500px">
      <el-upload
        action="http://localhost:5000/api/images/upload"
        :headers="{ Authorization: `Bearer ${localStorage.getItem('token')}` }"
        :on-success="handleUploadSuccess"
        :on-error="handleUploadError"
        :show-file-list="true"
        accept="image/*"
        :auto-upload="false"
        ref="uploadRef"
      >
        <el-button type="primary" @click="$refs.uploadRef.submit()">上传</el-button>
        <el-button @click="showUploadDialog = false">取消</el-button>
        <template #trigger>
          <el-button>选择文件</el-button>
        </template>
      </el-upload>
    </el-dialog>
    
    <!-- 图片预览对话框 -->
    <el-dialog v-model="previewVisible" title="图片预览" width="800px">
      <img v-if="previewImageUrl" :src="previewImageUrl" alt="预览图片" style="width: 100%;">
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import axios from 'axios'

// 图片列表
const images = ref([])
// 上传对话框显示状态
const showUploadDialog = ref(false)
// 预览对话框显示状态
const previewVisible = ref(false)
// 预览图片URL
const previewImageUrl = ref('')
// 上传组件引用
const uploadRef = ref(null)

// 获取图片列表
const fetchImages = async () => {
  try {
    const response = await axios.get('http://localhost:5000/api/images', {
      headers: {
        Authorization: `Bearer ${localStorage.getItem('token')}`
      }
    })
    images.value = response.data
  } catch (error) {
    console.error('获取图片列表失败:', error)
    alert('获取图片列表失败')
  }
}

// 格式化日期
const formatDate = (dateString) => {
  const date = new Date(dateString)
  return date.toLocaleString()
}

// 获取图片URL
const getImageUrl = (filepath) => {
  return `http://localhost:5000${filepath}`
}

// 预览图片
const previewImage = (image) => {
  previewImageUrl.value = getImageUrl(image.filepath)
  previewVisible.value = true
}

// 删除图片
const handleDelete = async (imageId) => {
  try {
    await axios.delete(`http://localhost:5000/api/images/delete/${imageId}`, {
      headers: {
        Authorization: `Bearer ${localStorage.getItem('token')}`
      }
    })
    // 重新获取图片列表
    fetchImages()
    alert('图片删除成功')
  } catch (error) {
    console.error('删除图片失败:', error)
    alert('删除图片失败')
  }
}

// 上传成功处理
const handleUploadSuccess = () => {
  showUploadDialog.value = false
  fetchImages()
  alert('图片上传成功')
}

// 上传失败处理
const handleUploadError = () => {
  alert('图片上传失败')
}

// 组件挂载时获取图片列表
onMounted(() => {
  fetchImages()
})
</script>

<style scoped>
.image-gallery-container {
  padding: 20px;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.image-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 20px;
}

.image-card {
  height: 300px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.image-content {
  height: 200px;
  display: flex;
  justify-content: center;
  align-items: center;
  overflow: hidden;
}

.image-content img {
  max-width: 100%;
  max-height: 100%;
  cursor: pointer;
  transition: transform 0.3s;
}

.image-content img:hover {
  transform: scale(1.05);
}

.image-info {
  margin-top: 10px;
  font-size: 12px;
  color: #606266;
}
</style>