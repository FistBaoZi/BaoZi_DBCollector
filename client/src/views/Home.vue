<template>
  <a-layout class="layout">
    <a-layout-header class="header">
      <div class="logo">数据采集系统</div>
      <a-menu
        v-model:selectedKeys="selectedKeys"
        theme="light"
        mode="horizontal"
        class="menu"
      >
        <a-menu-item key="dashboard">
          <router-link to="/dashboard">仪表盘</router-link>
        </a-menu-item>
        <a-menu-item key="station">
          <router-link to="/station">厂站管理</router-link>
        </a-menu-item>
        <!-- <a-menu-item key="report">
          <router-link to="/report">报表与分析</router-link>
        </a-menu-item>
        <a-menu-item key="api">
          <router-link to="/api">API管理</router-link>
        </a-menu-item> -->
        <a-menu-item key="license">
          <router-link to="/license">授权管理</router-link>
        </a-menu-item>
        <a-menu-item key="donate">
          <router-link to="/donate">捐赠支持</router-link>
        </a-menu-item>
      </a-menu>
    </a-layout-header>

    <a-layout-content class="content">
      <router-view></router-view>
    </a-layout-content>
  </a-layout>
</template>

<script setup>
import { ref, watch } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const selectedKeys = ref([])

// 根据当前路由路径更新菜单选中状态
const updateSelectedKeys = () => {
  const path = route.path.split('/')[1] // 获取路由的第一段
  selectedKeys.value = path ? [path] : ['dashboard']
}

// 监听路由变化
watch(() => route.path, updateSelectedKeys, { immediate: true })
</script>

<style scoped>
.layout {
  min-height: 100vh;
}

.header {
  display: flex;
  align-items: center;
  background: #fff;
  padding: 0 24px;
  box-shadow: 0 2px 8px #f0f1f2;
}

.logo {
  font-size: 18px;
  font-weight: bold;
  margin-right: 48px;
}

.menu {
  flex: 1;
  line-height: 64px;
}

.content {
  padding: 24px;
  background: #fff;
  margin: 24px;
}
</style>
