<template>
  <div class="login-container">
    <a-card title="数据采集系统" class="login-card">
      <a-form
        :model="formState"
        name="login"
        autocomplete="off"
        @finish="handleSubmit"
      >
        <a-form-item
          name="username"
          :rules="[{ required: true, message: '请输入用户名' }]"
        >
          <a-input v-model:value="formState.username" placeholder="用户名">
            <template #prefix>
              <UserOutlined />
            </template>
          </a-input>
        </a-form-item>

        <a-form-item
          name="password"
          :rules="[{ required: true, message: '请输入密码' }]"
        >
          <a-input-password v-model:value="formState.password" placeholder="密码">
            <template #prefix>
              <LockOutlined />
            </template>
          </a-input-password>
        </a-form-item>

        <a-form-item
          name="captcha"
          :rules="[{ required: true, message: '请输入验证码' }]"
        >
          <a-row :gutter="8">
            <a-col :span="16">
              <a-input v-model:value="formState.captcha" placeholder="验证码" />
            </a-col>
            <a-col :span="8">
              <img
                :src="captchaUrl"
                class="captcha-img"
                @click="refreshCaptcha"
                alt="验证码"
              />
            </a-col>
          </a-row>
        </a-form-item>

        <a-form-item>
          <a-button type="primary" html-type="submit" block :loading="loading">
            登录
          </a-button>
        </a-form-item>
      </a-form>
    </a-card>
  </div>
</template>

<script setup>
import { reactive, ref, inject, onMounted } from 'vue'
import { UserOutlined, LockOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const global = inject('global')
const loading = ref(false)
const captchaId = ref('')

const formState = reactive({
  username: '',
  password: '',
  captcha: '',
})

const captchaUrl = ref('')

const refreshCaptcha = () => {
  captchaId.value = Date.now().toString()
  captchaUrl.value = `/User/Captcha?seed=${captchaId.value}`
}

const handleSubmit = async () => {
  loading.value = true
  const res = await global.post('/User/Login', {
      ...formState,
      captchaId: captchaId.value,
    })
    if (res.token) {
      console.log(res);
      
      localStorage.setItem('token', res.token)
      localStorage.setItem('user', JSON.stringify(res.user))
      message.success('登录成功')
      router.push('/')
    }else{
      message.error('登录失败'+res)
    }
    loading.value = false
}

onMounted(() => {
  refreshCaptcha()
})
</script>

<style scoped>
.login-container {
  height: 100vh;
  display: flex;
  justify-content: center;
  align-items: center;
  background: #f0f2f5;
}

.login-card {
  width: 100%;
  max-width: 380px;
}

.captcha-img {
  width: 100%;
  height: 32px;
  cursor: pointer;
}
</style>
