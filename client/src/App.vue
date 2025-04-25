<template>
  <a-config-provider :locale="locale">
    <router-view></router-view>
  </a-config-provider>
</template>

<script setup>
import { ref } from 'vue'
import zhCN from 'ant-design-vue/es/locale/zh_CN'

const locale = ref(zhCN)
</script>

<style>
#app {
  font-family: Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  height: 100vh;
}
.tableWrapper table tr td{
  border: 1px solid #929191;
}
.tableWrapper table tr th{
  border: 1px solid #929191;
}
.tableWrapper table tbody tr th p{
  margin: 0 auto;
}
.tableWrapper table tbody tr td p{
  margin: 0 auto;
}

</style>
