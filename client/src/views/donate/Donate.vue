<template>
  <div class="donate-container">
    <a-card title="感谢支持" :bordered="false">
      <p>如果您觉得这个项目对您有帮助，欢迎赞助支持项目的持续开发。</p>
      <div class="donate-methods">
        <a-card title="支付宝" class="donate-card">
          <img src="@/assets/donate_blue.jpg" alt="支付宝收款码" class="qrcode-image" />
        </a-card>
        <a-card title="微信支付" class="donate-card">
          <img src="@/assets/donate_green.jpg" alt="微信收款码" class="qrcode-image" />
        </a-card>
      </div>
    </a-card>
  </div>
</template>

<style scoped>
.donate-container {
  max-width: 1200px;
  margin: 0 auto;
}

.donate-methods {
  display: flex;
  justify-content: center;
  gap: 24px;
  margin-top: 24px;
}

.donate-card {
  width: 300px;
  text-align: center;
}

.qrcode-image {
  width: 200px;
  height: 200px;
  object-fit: contain;
  margin: 16px auto;
  display: block;
}
</style>
