<template>
  <div class="station-manager">
    <div class="table-header">
      <h2>厂站管理</h2>
      <a-space>
        <a-button type="primary" @click="showStationModal()">添加厂站</a-button>
        <a-button type="primary" @click="showBatchCollectModal()">批量添加采集</a-button>
      </a-space>
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

    <!-- 厂站表单弹窗 -->
    <a-modal
      v-model:open="stationModal.visible"
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
      :width="1200"
      v-model:open="collectDrawer.visible"
      @close="closeCollectDrawer"
    >
      <template #extra>
        <a-button type="primary" @click="saveCollectConfig" :loading="collectDrawer.saving">
          保存配置
        </a-button>
      </template>
      <div>
        <a-transfer
          v-model:target-keys="collectDrawer.selectedTables"
          :data-source="collectDrawer.allTables"
          :show-search="true"
          :filter-option="filterOption"
          :render="item => item.title"
          @change="handleTransferChange"
          :titles="['未配置采集的表', '已配置采集的表']"
        >
        </a-transfer>
      </div>
    </a-drawer>

    <!-- 批量采集配置弹窗 -->
    <a-modal
      v-model:open="batchCollectModal.visible"
      title="批量添加采集配置"
      width="1000px"
      @ok="handleBatchCollectSubmit"
      :confirmLoading="batchCollectModal.saving"
    >
      <div class="batch-collect-container">
        <div class="collect-columns">
          <div class="collect-column">
            <div class="column-header">
              <div class="column-title">选择厂站</div>
              <a-input-search
                v-model:value="batchCollectModal.stationSearchText"
                placeholder="搜索厂站"
                style="width: 200px"
              />
            </div>
            <div class="checkbox-container">
              <a-checkbox-group
                v-model:value="batchCollectModal.selectedStations"
                class="checkbox-list"
                @change="handleStationCheckboxChange"
              >
                <div v-for="station in filteredStations" :key="station.StationId" class="checkbox-item">
                  <a-checkbox :value="station.StationId">{{ station.Name }}</a-checkbox>
                </div>
              </a-checkbox-group>
            </div>
          </div>
          <div class="collect-column">
            <div class="column-header">
              <div class="column-title">选择需要采集的表</div>
              <a-input-search
                v-model:value="batchCollectModal.tableSearchText"
                placeholder="搜索数据表"
                style="width: 200px"
              />
            </div>
            <div class="checkbox-container">
              <div v-if="batchCollectModal.loading" class="loading-container">
                <a-spin tip="加载表数据中..." />
              </div>
              <a-checkbox-group
                v-else
                v-model:value="batchCollectModal.selectedTables"
                class="checkbox-list"
              >
                <div v-for="table in filteredTables" :key="table.name" class="checkbox-item">
                  <a-checkbox :value="table.name">
                    {{ table.name }}
                    <span class="table-description" v-if="table.description">({{ table.description }})</span>
                  </a-checkbox>
                </div>
              </a-checkbox-group>
            </div>
          </div>
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, inject, computed } from 'vue'
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
    console.log(res.allTables);
    
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

// 批量采集配置相关
const batchCollectModal = reactive({
  visible: false,
  saving: false,
  selectedStations: [],
  stationList: [],
  allTables: [],
  selectedTables: [],
  prevFirstStation: null, // 用于记录之前的第一个选中的厂站
  stationSearchText: '',
  tableSearchText: '',
  loading: false,
})

const showBatchCollectModal = () => {
  batchCollectModal.visible = true
  batchCollectModal.stationList = stations.value
  batchCollectModal.selectedStations = []
  batchCollectModal.selectedTables = []
  batchCollectModal.allTables = []
  batchCollectModal.prevFirstStation = null
}

const handleStationCheckboxChange = async (checkedValues) => {
  const firstStation = checkedValues[0]
  // 当第一个选中的厂站发生变化时，重新加载表列表
  if (firstStation && firstStation !== batchCollectModal.prevFirstStation) {
    batchCollectModal.prevFirstStation = firstStation
    await fetchBatchTableList(firstStation)
  }
  // 当没有选中的厂站时，清空表列表
  if (checkedValues.length === 0) {
    batchCollectModal.allTables = []
    batchCollectModal.selectedTables = []
    batchCollectModal.prevFirstStation = null
  }
}

const fetchBatchTableList = async (stationId) => {
  batchCollectModal.loading = true
  try {
    const res = await global.get(`/DBCollector/GetTableList`, { 
      params: { stationId }
    })
    batchCollectModal.allTables = res.allTables
    batchCollectModal.selectedTables = []
  } catch (error) {
    message.error('获取数据表列表失败')
  } finally {
    batchCollectModal.loading = false
  }
}

const handleBatchCollectSubmit = async () => {
  if (batchCollectModal.selectedStations.length === 0) {
    message.warning('请至少选择一个厂站')
    return
  }
  if (batchCollectModal.selectedTables.length === 0) {
    message.warning('请至少选择一个数据表')
    return
  }

  batchCollectModal.saving = true
  try {
    await global.post('/DBCollector/BatchSaveCollectConfig', {
      stationIds: batchCollectModal.selectedStations,
      tables: batchCollectModal.selectedTables
    })
    message.success('批量配置成功')
    batchCollectModal.visible = false
  } catch (error) {
    message.error('批量配置失败')
  } finally {
    batchCollectModal.saving = false
  }
}

// 添加搜索过滤计算属性
const filteredStations = computed(() => {
  const searchText = batchCollectModal.stationSearchText.toLowerCase()
  return batchCollectModal.stationList.filter(station => 
    station.Name.toLowerCase().includes(searchText) || 
    station.StationId.toLowerCase().includes(searchText)
  )
})

const filteredTables = computed(() => {
  const searchText = batchCollectModal.tableSearchText.toLowerCase()
  return batchCollectModal.allTables.filter(table =>
    table.name.toLowerCase().includes(searchText) ||
    (table.description && table.description.toLowerCase().includes(searchText))
  )
})


onMounted(() => {
  fetchStations()
})
</script>

<style scoped>
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


:deep(.ant-drawer-body) {
  padding: 24px;
  height: calc(100% - 55px); /* 减去抽屉头部的高度 */
}



:deep(.ant-transfer-list) {
  flex: 1;
  height: 80vh;
  margin: 0;
}

:deep(.ant-transfer-operation) {
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

.batch-collect-container {
  height: 600px;
}

.collect-columns {
  display: flex;
  gap: 20px;
  height: 100%;
}

.collect-column {
  flex: 1;
  display: flex;
  flex-direction: column;
  border: 1px solid #e8e8e8;
  border-radius: 4px;
}

.column-header {
  padding: 12px;
  border-bottom: 1px solid #e8e8e8;
  background-color: #fafafa;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.column-title {
  font-weight: 500;
  color: #1f1f1f;
}

.checkbox-container {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
}

.checkbox-list {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
}

.checkbox-item {
  margin-bottom: 8px;
}

:deep(.ant-checkbox-wrapper) {
  margin-left: 0 !important;
  text-align: left;
}

.table-description {
  color: #999;
  font-size: 12px;
  margin-left: 4px;
}

:deep(.ant-checkbox-group) {
  display: flex;
  flex-direction: column;
}

.loading-container {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 100%;
}
</style>