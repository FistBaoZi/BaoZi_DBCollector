<template>
  <div class="license-management">
    <a-card title="授权管理">
      <a-row :gutter="[16, 16]">
        <a-col :span="24">
          <a-space>
            <a-upload
              :custom-request="handleUpload"
              :show-upload-list="false"
              accept=".lic"
            >
              <a-button type="primary">
                <upload-outlined />
                上传License文件
              </a-button>
            </a-upload>
            <a-button 
              v-if="canCreateLicense" 
              type="primary" 
              @click="showCreateModal"
            >
              <plus-outlined />
              创建证书
            </a-button>
          </a-space>
        </a-col>
        <a-col :span="24">
          <div class="license-info">
            <template v-if="licenseInfo.status">
              <p><strong>授权状态：</strong>{{ licenseInfo.isExpired ? '已过期' : '已授权' }}</p>
              <p><strong>授权开始时间：</strong>{{ licenseInfo.startDate }}</p>
              <p><strong>授权结束时间：</strong>{{ licenseInfo.endDate }}</p>
              <p><strong>授权厂站数量：</strong>{{ licenseInfo.maxStations }}</p>
              <p><strong>授权天数：</strong>{{ licenseInfo.authDays }}</p>
              <p><strong>证书创建时间：</strong>{{ licenseInfo.createTime }}</p>
            </template>
            <template v-else>
              <p class="no-license">{{ licenseInfo.message || '未安装授权证书' }}</p>
            </template>
          </div>
        </a-col>
      </a-row>
    </a-card>

    <!-- 创建证书表单模态框 -->
    <a-modal
      v-model:visible="createModalVisible"
      title="创建证书"
      @ok="handleCreateLicense"
      @cancel="createModalVisible = false"
    >
      <a-form
        :model="createForm"
        :rules="rules"
        ref="createFormRef"
        :label-col="{ span: 6 }"
        :wrapper-col="{ span: 16 }"
      >
        <a-form-item name="authDays" label="授权天数">
          <a-input-number 
            v-model:value="createForm.authDays" 
            :min="1"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item name="maxStations" label="授权厂站个数">
          <a-input-number 
            v-model:value="createForm.maxStations" 
            :min="1"
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item name="startDate" label="授权开始时间">
          <a-date-picker 
            v-model:value="createForm.startDate"
            show-time
            style="width: 100%"
          />
        </a-form-item>
        <a-form-item name="endDate" label="授权结束时间">
          <a-date-picker 
            v-model:value="createForm.endDate"
            show-time
            style="width: 100%"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup>
import { ref, inject, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { UploadOutlined, PlusOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'

const global = inject('global')
const licenseInfo = ref({
  status: false,
  expiryDate: '',
  type: ''
})

// 新增的响应式变量
const canCreateLicense = ref(false)
const createModalVisible = ref(false)
const createFormRef = ref(null)
const createForm = ref({
  authDays: 365,
  maxStations: 1,
  startDate: null,
  endDate: null
})

// 表单验证规则
const rules = {
  authDays: [{ required: true, message: '请输入授权天数' }],
  maxStations: [{ required: true, message: '请输入授权厂站个数' }],
  startDate: [{ required: true, message: '请选择授权开始时间' }],
  endDate: [{ required: true, message: '请选择授权结束时间' }]
}

const handleUpload = async ({ file }) => {
  const formData = new FormData()
  formData.append('file', file)
  
  try {
    const res = await global.post('/License/Upload', formData,{
         transformRequest: null
    })
    if (res.success) {
      message.success('License上传成功')
      await getLicenseInfo()
    } else {
      message.error(res.message || '上传失败')
    }
  } catch (error) {
    message.error('上传失败：' + error.message)
  }
}

const getLicenseInfo = async () => {
  try {
    const res = await global.get('/License/GetLicenseInfo')
    licenseInfo.value = res
  } catch (error) {
    message.error('获取授权信息失败：' + error.message)
  }
}

// 检查是否可以创建证书
const checkCanCreateLicense = async () => {
  try {
     
    const res = await global.post('/License/CanCreate')
    canCreateLicense.value = res
  } catch {
  }
}

// 监听授权天数变化，自动更新结束日期
watch(() => createForm.value.authDays, (newDays) => {
  if (createForm.value.startDate) {
    createForm.value.endDate = dayjs(createForm.value.startDate)
      .startOf('day')
      .add(newDays, 'day')
      .startOf('day')
  }
})

// 监听开始日期变化，同步更新结束日期
watch(() => createForm.value.startDate, (newStartDate) => {
  if (newStartDate && createForm.value.authDays) {
    createForm.value.endDate = dayjs(newStartDate)
      .startOf('day')
      .add(createForm.value.authDays, 'day')
      .startOf('day')
  }
})

// 显示创建证书模态框
const showCreateModal = () => {
  createModalVisible.value = true
  const today = dayjs().startOf('day')
  createForm.value = {
    authDays: 365,
    maxStations: 3,
    startDate: today,
    endDate: today.add(365, 'day').startOf('day')
  }
}

// 处理创建证书
const handleCreateLicense = async () => {
  try {
    await createFormRef.value.validate()
    const formData = {
      ...createForm.value,
      startDate: createForm.value.startDate.format('YYYY-MM-DD HH:mm:ss'),
      endDate: createForm.value.endDate.format('YYYY-MM-DD HH:mm:ss')
    }
    
    const res = await global.post('/License/Create', { req: formData }, { 
      responseType: 'blob' 
    })
    
    // 创建blob链接并下载
    const blob = new Blob([res], { type: 'application/octet-stream' })
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'license.lic'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)

    message.success('证书创建成功')
    createModalVisible.value = false
    await getLicenseInfo()
  } catch (error) {
    message.error('创建失败：' + error.message)
  }
}

onMounted(() => {
  getLicenseInfo()
  checkCanCreateLicense()
})
</script>

<style scoped>
.license-management {
  padding: 24px;
}
.license-info {
  padding: 16px;
  background: #fafafa;
  border-radius: 4px;
}
.license-info p {
  margin-bottom: 8px;
  line-height: 24px;
}
.license-info p:last-child {
  margin-bottom: 0;
}
.no-license {
  color: #ff4d4f;
  text-align: center;
  font-size: 16px;
}
</style>
