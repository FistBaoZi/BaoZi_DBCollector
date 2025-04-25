import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src')
    }
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, '')
      }
    },
    open: true // 自动打开浏览器
  },
  base: '/', // 修改这里，使用相对路径
  build: {
    outDir: '../api/wwwroot', // 设置输出目录
    emptyOutDir: true, // 构建前清空输出目录
  }
})
