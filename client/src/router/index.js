import { createRouter, createWebHashHistory } from 'vue-router'
const routes = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('../views/Login.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/',
    component: () => import('../views/Home.vue'),
    children: [
      {
        path: '',
        redirect: '/dashboard'
      },
      {
        path: 'dashboard',
        name: 'Dashboard',
        component: () => import('../views/dashboard/Dashboard.vue'),
        meta: { title: '仪表盘' }
      },
      {
        path: 'station',
        name: 'Station',
        component: () => import('../views/station/StationManager.vue'),
        meta: { title: '厂站管理' }
      },
      {
        path: 'report',
        name: 'Report',
        component: () => import('../views/report/Report.vue'),
        meta: { title: '报表与分析' }
      },
      {
        path: 'api',
        name: 'ApiManager',
        component: () => import('../views/api/ApiManager.vue'),
        meta: { title: 'API管理' }
      },
      {
        path: 'license',
        name: 'License',
        component: () => import('../views/LicenseManagement.vue'),
        meta: { title: '授权管理' }
      },
      {
        path: 'donate',
        name: 'Donate',
        component: () => import('../views/donate/Donate.vue'),
        meta: { title: '捐赠支持' }
      }
    ]
  },
  {
    path: '/404',
    name: '404',
    component: () => import('../views/404.vue')
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/404'
  }
]

const router = createRouter({
  history: createWebHashHistory(),
  routes
})

// 路由守卫
router.beforeEach((to, from, next) => {
  const token = localStorage.getItem('token')
  // 处理需要认证的路由
  if (to.meta.requiresAuth && !token) {
    next({ path: '/login', query: { redirect: to.fullPath } })
    return
  }
  next()
})

export default router
