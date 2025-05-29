<template>
  <div class="donate-container">
    <h1>捐赠支持</h1>
    <p>感谢您对我们项目的支持！</p>
    <div class="donate-methods">
      <a-card title="支付宝捐赠" style="width: 300px; margin: 16px">
        <!-- 这里放支付宝收款码 -->
      </a-card>
      <a-card title="微信捐赠" style="width: 300px; margin: 16px">
        <!-- 这里放微信收款码 -->
      </a-card>
    </div>
  </div>
</template>

<style scoped>
.donate-container {
  padding: 24px;
}
.donate-methods {
  display: flex;
  justify-content: center;
  flex-wrap: wrap;
  margin-top: 24px;
}
</style>
