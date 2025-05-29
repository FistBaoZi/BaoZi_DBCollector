<template>
  <div class="dashboard">
    <h2>采集日志</h2>
    
    <!-- 搜索条件 -->
    <div class="search-bar">
      <a-form layout="inline">
        <a-form-item label="厂站">
          <a-select v-model:value="searchForm.stationId" style="width: 200px" allowClear>
            <a-select-option v-for="station in stations" :key="station.StationId" :value="station.StationId">
              {{ station.Name }}
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="日志级别">
          <a-select v-model:value="searchForm.logLevel" style="width: 120px" allowClear>
            <a-select-option value="INFO">INFO</a-select-option>
            <a-select-option value="ERROR">ERROR</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="loadLogs">查询</a-button>
        </a-form-item>
      </a-form>
    </div>

    <!-- 日志列表 -->
    <a-table :dataSource="logs.rows" :columns="columns" :loading="loading"
             :pagination="{
               total: logs.total,
               current: searchForm.page,
               pageSize: searchForm.pageSize,
               onChange: handlePageChange,
               showSizeChanger: true,
               showTotal: total => `共 ${total} 条`
             }">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'logLevel'">
          <a-tag :color="record.logLevel === 'ERROR' ? 'red' : 'blue'">
            {{ record.logLevel }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'createTime'">
          {{ formatDateTime(record.createTime) }}
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup>
import { ref, onMounted, inject } from 'vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'

const global = inject('global')
const stations = ref([])
const loading = ref(false)
const logs = ref({ rows: [], total: 0 })

const searchForm = ref({
  stationId: undefined,
  logLevel: undefined,
  page: 1,
  pageSize: 10
})

const columns = [
  {
    title: '厂站名称',
    dataIndex: 'StationName',
    width: 150
  },
  {
    title: '表名',
    dataIndex: 'TableName',
    width: 200
  },
  {
    title: '级别',
    dataIndex: 'LogLevel',
    key: 'LogLevel',
    width: 100
  },
  {
    title: '日志内容',
    dataIndex: 'Message',
    key: 'Message',
    ellipsis: true
  },
  {
    title: '时间',
    dataIndex: 'CreateTime',
    key: 'CreateTime',
    width: 180
  }
]

const loadStations = async () => {
  try {
    const res = await global.get('/DBCollector/List')
    stations.value = res
  } catch (error) {
    message.error('加载厂站列表失败')
  }
}

const loadLogs = async () => {
  loading.value = true
  try {
    const res = await global.get('/DBCollector/GetCollectorLogs', {
      params: searchForm.value
    })
    logs.value = res
  } catch (error) {
    message.error('加载日志失败')
  } finally {
    loading.value = false
  }
}

const handlePageChange = (page, pageSize) => {
  searchForm.value.page = page
  searchForm.value.pageSize = pageSize
  loadLogs()
}

const formatDateTime = (date) => {
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

onMounted(() => {
  loadStations()
  loadLogs()
})
</script>

<style scoped>
.dashboard {
  padding: 20px;
}
.search-bar {
  margin-bottom: 20px;
}
</style>
