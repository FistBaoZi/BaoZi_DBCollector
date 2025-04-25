<template>
  <a-layout class="layout">
    <a-layout-header class="header">
      <div class="logo">数据采集系统</div>
    </a-layout-header>

    <a-layout-content class="content">
      <div class="table-header">
        <h2>厂站管理</h2>
        <a-button type="primary" @click="showStationModal()">
          添加厂站
        </a-button>
      </div>

      <a-table :columns="columns" :data-source="stations" :loading="loading">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="showStationModal(record)">编辑</a-button>
              <a-button type="link" @click="showCollectConfig(record)">采集配置</a-button>
              <a-popconfirm
                title="确定要删除这个厂站吗？"
                @confirm="deleteStation(record.Id)"
              >
                <a-button type="link" danger>删除</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-layout-content>

    <!-- 厂站表单弹窗 -->
    <a-modal
      v-model:visible="stationModal.visible"
      :title="stationModal.isEdit ? '编辑厂站' : '添加厂站'"
      @ok="handleStationSubmit"
    >
      <a-form :model="stationForm" :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
        <a-form-item label="厂站名称" required>
          <a-input v-model:value="stationForm.Name" />
        </a-form-item>
        <a-form-item label="厂站ID" required>
          <a-input v-model:value="stationForm.StationId" />
        </a-form-item>
        <a-form-item label="连接字符串" required>
          <a-input v-model:value="stationForm.ConnectionString" :rows="3" />
          <div class="form-item-help">示例：Data Source=.;Initial Catalog=DBName;User ID=sa;password=123456;</div>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 采集配置抽屉 -->
    <a-drawer
      :title=" collectDrawer.currentStation?.Name +'采集配置'"
      placement="right"
      :width="720"
      v-model:visible="collectDrawer.visible"
      @close="closeCollectDrawer"
    >
      <template #extra>
        <a-button type="primary" @click="saveCollectConfig" :loading="collectDrawer.saving">
          保存配置
        </a-button>
      </template>
      <div class="transfer-container">
        <a-transfer
          v-model:target-keys="collectDrawer.selectedTables"
          :data-source="collectDrawer.allTables"
          :show-search="true"
          :filter-option="filterOption"
          :render="item => item.title"
          @change="handleTransferChange"
          :titles="['未配置采集的表', '已配置采集的表']"
          class="custom-transfer"
        >
        </a-transfer>
      </div>
    </a-drawer>
  </a-layout>
</template>

<script setup>
import { ref, reactive, onMounted, inject } from 'vue'
import { message } from 'ant-design-vue'

const global = inject('global')
const loading = ref(false)
const stations = ref([])

const columns = [
  { title: '厂站名称', dataIndex: 'Name', key: 'Name' },
  { title: '厂站ID', dataIndex: 'StationId', key: 'StationId' },
  { title: '连接字符串', dataIndex: 'ConnectionString', key: 'ConnectionString', ellipsis: true },
  { title: '操作', key: 'action' }
]

// 厂站表单相关
const stationModal = reactive({
  visible: false,
  isEdit: false
})

const stationForm = reactive({
  Id: null,
  Name: '',
  StationId: '',
  ConnectionString: ''
})

// 获取厂站列表
const fetchStations = async () => {
  loading.value = true
  try {
    const res = await global.get('/DBCollector/List')
    stations.value = res
    console.log(res);
    
  } catch (error) {
    message.error('获取厂站列表失败')
  } finally {
    loading.value = false
  }
}

// 厂站表单处理
const showStationModal = (record = null) => {
  if (record) {
    Object.assign(stationForm, record)
    stationModal.isEdit = true
  } else {
    Object.assign(stationForm, {
      Id: null,
      Name: '',
      StationId: '',
      ConnectionString: ''
    })
    stationModal.isEdit = false
  }
  stationModal.visible = true
}

const handleStationSubmit = async () => {
  try {
    const url = stationModal.isEdit ? '/DBCollector/Update' : '/DBCollector/Add'
    await global.post(url, stationForm)
    message.success(`${stationModal.isEdit ? '更新' : '添加'}成功`)
    stationModal.visible = false
    fetchStations()
  } catch (error) {
    message.error(`${stationModal.isEdit ? '更新' : '添加'}失败`)
  }
}

const deleteStation = async (id) => {
  try {
    await global.post('/DBCollector/Delete', { id: id })
    message.success('删除成功')
    fetchStations()
  } catch (error) {
    message.error('删除失败')
  }
}

// 采集配置相关
const collectDrawer = reactive({
  visible: false,
  selectedTables: [],
  allTables: [],
  currentStation: null,
  saving: false
})

const showCollectConfig = async (record) => {
  collectDrawer.visible = true
  collectDrawer.currentStation = record
  await fetchTableList(record)
}

const closeCollectDrawer = () => {
  collectDrawer.visible = false
  collectDrawer.selectedTables = []
  collectDrawer.currentStation = null
}

const fetchTableList = async (station) => {
  try {
    const res = await global.get(`/DBCollector/GetTableList`, { 
      params: { stationId: station.StationId }
    })
    // 转换数据格式用于穿梭框
    collectDrawer.allTables = res.allTables.map(table => ({
      key: table.name,
      title: table.name,
      description: table.description
    }))
    collectDrawer.selectedTables = res.selectedTables || []
  } catch (error) {
    message.error('获取数据表列表失败')
  }
}

const filterOption = (inputValue, item) => {
  return item.title.toLowerCase().indexOf(inputValue.toLowerCase()) !== -1
}

const handleTransferChange = (keys) => {
  collectDrawer.selectedTables = keys
}

const saveCollectConfig = async () => {
  if (!collectDrawer.currentStation) return
  
  collectDrawer.saving = true
  try {
    await global.post('/DBCollector/SaveCollectConfig', {
      stationId: collectDrawer.currentStation.StationId,
      tables: collectDrawer.selectedTables
    })
    message.success('保存成功')
    collectDrawer.visible = false
  } catch (error) {
    message.error('保存失败')
  } finally {
    collectDrawer.saving = false
  }
}

onMounted(() => {
  fetchStations()
})
</script>

<style scoped>
.layout {
  min-height: 100vh;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #fff;
  padding: 0 24px;
  box-shadow: 0 2px 8px #f0f1f2;
}

.logo {
  font-size: 18px;
  font-weight: bold;
}

.content {
  padding: 24px;
  background: #fff;
  margin: 24px;
}

.table-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.form-item-help {
  color: #999;
  font-size: 12px;
  margin-top: 4px;
}

.ant-transfer {
  justify-content: center;
  margin: 24px 0;
}

:deep(.ant-drawer-body) {
  padding: 24px;
  height: calc(100% - 55px); /* 减去抽屉头部的高度 */
}

:deep(.custom-transfer) {
  height: 100%;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

:deep(.ant-transfer-list) {
  flex: 1;
  width: 48% !important;
  height: 80vh;
  margin: 0;
}

:deep(.ant-transfer-operation) {
  width: 4%;
  padding: 0 8px;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

:deep(.ant-transfer-list-body) {
  height: calc(100% - 94px); /* 减去标题、搜索框和其他padding的高度 */
}

:deep(.ant-transfer-list-body-customize-wrapper) {
  height: 100%;
}

/* 删除不需要的样式 */
.collect-transfer-container {
  display: none;
}

.transfer-container {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.transfer-title {
  margin-bottom: 16px;
  color: #1890ff;
}

:deep(.transfer-list-title) {
  font-size: 14px;
  font-weight: 500;
  color: #1f1f1f;
}

:deep(.ant-transfer-list-header) {
  padding: 8px 12px !important;
}
</style>
