# Vue3 + Flask 登录与图片展示系统前端开发文档

## 一、项目结构

```
vue_test1/
├── public/            # 静态资源目录
│   ├── favicon.ico   # 网站图标
│   └── index.html    # HTML 入口文件
├── src/              # 源代码目录
│   ├── assets/       # 资源文件
│   │   └── logo.png  # Logo 图片
│   ├── components/   # Vue 组件
│   │   ├── Login.vue        # 登录组件
│   │   └── ImageGallery.vue # 图片展示组件
│   ├── router/       # 路由配置
│   │   └── index.js  # 路由定义
│   ├── stores/       # 状态管理（预留）
│   ├── utils/        # 工具函数（预留）
│   ├── App.vue       # 根组件
│   └── main.js       # 入口文件
├── .gitignore        # Git 忽略文件
├── babel.config.js   # Babel 配置
├── jsconfig.json     # JavaScript 配置
├── package.json      # 项目依赖和脚本
└── vue.config.js     # Vue CLI 配置
```

## 二、核心组件

### 1. 登录组件（Login.vue）

**功能**：实现用户登录功能，包括表单验证和登录请求处理。

**核心代码**：
```vue
<template>
  <div class="login-container">
    <div class="login-form-wrapper">
      <h2>登录</h2>
      <el-form :model="loginForm" :rules="rules" ref="loginFormRef" label-width="80px">
        <el-form-item label="用户名" prop="username">
          <el-input v-model="loginForm.username" placeholder="请输入用户名"></el-input>
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="loginForm.password" type="password" placeholder="请输入密码" show-password></el-input>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleLogin" :loading="loading">登录</el-button>
        </el-form-item>
      </el-form>
    </div>
  </div>
</template>
```

**主要逻辑**：
- 表单验证：使用 Element Plus 的表单验证规则
- 登录请求：发送 POST 请求到 `/api/login`
- Token 存储：登录成功后将 token 存储到本地存储
- 路由跳转：登录成功后跳转到图片展示页

### 2. 图片展示组件（ImageGallery.vue）

**功能**：实现图片的展示、上传和删除功能。

**核心代码**：
```vue
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
        <!-- 卡片内容 -->
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
        <!-- 上传组件内容 -->
      </el-upload>
    </el-dialog>
  </div>
</template>
```

**主要逻辑**：
- 图片列表获取：发送 GET 请求到 `/api/images`
- 图片上传：使用 Element Plus 的 Upload 组件，发送 POST 请求到 `/api/images/upload`
- 图片删除：发送 DELETE 请求到 `/api/images/delete/{id}`
- 图片预览：点击图片可以预览大图

## 三、路由配置

**文件**：`src/router/index.js`

**核心代码**：
```javascript
const routes = [
  {
    path: '/login',
    name: 'Login',
    component: Login
  },
  {
    path: '/',
    name: 'ImageGallery',
    component: ImageGallery,
    meta: { requiresAuth: true } // 需要认证的路由
  },
  {
    path: '/logout',
    name: 'Logout',
    beforeEnter: (to, from, next) => {
      localStorage.removeItem('token')
      next('/login')
    }
  }
]

// 路由守卫
router.beforeEach((to, from, next) => {
  const token = localStorage.getItem('token')
  
  if (to.matched.some(record => record.meta.requiresAuth) && !token) {
    next('/login')
  } else {
    next()
  }
})
```

**路由说明**：
- `/login`：登录页，不需要认证
- `/`：图片展示页，需要认证
- `/logout`：登出，清除 token 并跳转到登录页

**路由守卫**：
- 检查路由是否需要认证
- 如果需要认证且没有 token，跳转到登录页

## 四、Axios 配置

**文件**：`src/main.js`

**核心代码**：
```javascript
// 请求拦截器
axios.interceptors.request.use(
  config => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  error => {
    return Promise.reject(error)
  }
)

// 响应拦截器
axios.interceptors.response.use(
  response => {
    return response
  },
  error => {
    if (error.response && error.response.status === 401) {
      localStorage.removeItem('token')
      router.push('/login')
    }
    return Promise.reject(error)
  }
)
```

**拦截器说明**：
- **请求拦截器**：自动添加 Authorization 头
- **响应拦截器**：处理 401 错误，清除 token 并跳转到登录页

## 五、状态管理

本项目预留了 Pinia 状态管理，但目前使用本地存储（localStorage）存储 token，以简化实现。

## 六、样式设计

### 1. 全局样式

**文件**：`src/App.vue`

**核心代码**：
```css
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: Avenir, Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  background-color: #f5f7fa;
}

#app {
  width: 100%;
  min-height: 100vh;
}
```

### 2. 登录页面样式

**文件**：`src/components/Login.vue`

**核心样式**：
- 居中布局：使用 flex 布局将登录表单居中显示
- 卡片样式：登录表单使用卡片式设计，带有阴影效果
- 响应式设计：适配不同屏幕尺寸

### 3. 图片展示页面样式

**文件**：`src/components/ImageGallery.vue`

**核心样式**：
- 网格布局：图片使用 grid 布局，自动适应屏幕宽度
- 卡片设计：每张图片使用卡片式设计，包含图片信息和操作按钮
- 图片悬停效果：鼠标悬停在图片上时有缩放效果

## 七、开发与构建

### 1. 安装依赖

```bash
npm install
```

### 2. 启动开发服务器

```bash
npm run serve
```

### 3. 构建生产版本

```bash
npm run build
```

### 4. 代码检查

```bash
npm run lint
```

## 八、注意事项

1. **跨域问题**：前端开发服务器默认会处理跨域问题，生产环境需要在后端配置 CORS
2. **Token 管理**：token 存储在 localStorage 中，刷新页面不会丢失
3. **表单验证**：前端实现了基本的表单验证，后端也会进行验证
4. **文件上传**：只允许上传图片类型的文件，大小限制为 16MB
5. **响应式设计**：适配不同屏幕尺寸

## 九、扩展建议

1. 添加用户注册功能
2. 实现图片分类和搜索功能
3. 添加图片编辑功能（裁剪、旋转等）
4. 实现图片分享功能
5. 添加用户头像和个人信息管理
6. 实现批量上传和批量删除功能
7. 添加图片点赞和评论功能
8. 实现图片云存储（如阿里云 OSS、AWS S3 等）