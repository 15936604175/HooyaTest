import { createRouter, createWebHistory } from 'vue-router'
import Login from '../components/Login.vue'
import ImageGallery from '../components/ImageGallery.vue'

// 路由配置
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
      // 清除本地存储的token
      localStorage.removeItem('token')
      // 跳转到登录页
      next('/login')
    }
  }
]

const router = createRouter({
  history: createWebHistory(process.env.BASE_URL),
  routes
})

// 路由守卫，检查是否需要认证
router.beforeEach((to, from, next) => {
  // 获取token
  const token = localStorage.getItem('token')
  
  // 如果路由需要认证且没有token，跳转到登录页
  if (to.matched.some(record => record.meta.requiresAuth) && !token) {
    next('/login')
  } else {
    next()
  }
})

export default router