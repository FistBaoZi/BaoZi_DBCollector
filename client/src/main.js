import { createApp } from 'vue'
import Antd from 'ant-design-vue'
import zhCN from 'ant-design-vue/es/locale/zh_CN'
import App from './App.vue'
import router from './router'
import 'dayjs/locale/zh-cn'
import 'ant-design-vue/dist/reset.css'
import Qs from 'qs'
import axios from 'axios'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import isBetween from 'dayjs/plugin/isBetween'

const app = createApp(App)
app.use(router)
dayjs.extend(isBetween);

const getBasePrefix = () => {
  return import.meta.env.MODE === 'development' ? '/api' : ''
}

const service = axios.create({
  baseURL: getBasePrefix(),
  transformRequest: [function (data) {
    return Qs.stringify(data) ;
  }],
  withCredentials:true  
})
// 请求拦截器
service.interceptors.request.use(
  config => {
    const token = localStorage.getItem('token')
    if (token) {
      // 确保 token 格式正确
      config.headers['Authorization'] = token.startsWith('Bearer ') ? token : `Bearer ${token}`
    }
    return config
  },
  error => {
    return Promise.reject(error)
  }
)

// 响应拦截器
service.interceptors.response.use(
  response => {
    return response.data
  },
  error => {
    if (error.response) {
      // 处理401未授权的情况
      if (error.response.status === 401) {
        localStorage.removeItem('token')
        localStorage.removeItem('userInfo')
        message.error('登录已过期，请重新登录')
        router.push('/login')
      }
    } else {
      message.error('网络连接失败')
    }
    return Promise.reject(error)
  }
)


app.config.globalProperties.get = service.get;
app.config.globalProperties.post = service.post;
app.config.globalProperties.getToken = function(){
  return localStorage.getItem('token')
};
app.config.globalProperties.getBasePrefix = getBasePrefix;

function getUserInfo(){
  return JSON.parse(localStorage.getItem('userInfo'))
}
app.config.globalProperties.getUserInfo = getUserInfo;
app.config.globalProperties.$dayjs = dayjs
app.provide('global',app.config.globalProperties)
app.use(Antd, {
  locale: zhCN,
})

app.mount('#app')
