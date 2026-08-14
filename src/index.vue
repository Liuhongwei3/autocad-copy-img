<template>
  <div class="app-container relative">
    <a-tabs
      :active-key="currentActiveKey"
      type="card"
      @change="handleTabChange"
    >
      <a-tab-pane
        key="ALL"
        tab="全部"
      />
      <a-tab-pane
        v-for="type in taskCardTypes"
        :key="type.value"
        :tab="type.text"
      />
    </a-tabs>

    <div class="flex items-center absolute top-[22px] left-[380px] z-[8]">
      <a-button
        type="primary"
        icon="plus-circle"
        @click="handleAddOrder"
      >
        新增工单
      </a-button>
      <div
        v-if="judgeHasOpr(currentActiveKey)"
        class="selected-wrapper h-full text-sm flex justify-between items-center ml-6"
      >
        <div>
          <template v-if="hasSelected">
            <span>{{ '已选中' + selectedRowKeys.length + '项工单' }}</span>
            <a-dropdown
              v-if="hasSelected"
              class="mr-4"
            >
              <a
                class="batch-operate-link"
                @click="(e) => e.preventDefault()"
              >
                <a-icon type="edit" />批量操作
              </a>

              <a-menu slot="overlay">
                <!-- <a-menu-item>
                <a @click="handleCreateCraftCard(selectedRowKeys)"
                  >批量生成工艺流转卡</a
                >
              </a-menu-item> -->
                <a-menu-item>
                  <a @click="handleCreatePlanCard(selectedRowKeys)">
                    工单批量打印
                  </a>
                </a-menu-item>
                <a-menu-item :disabled="cadExporting">
                  <a @click="downloadSelectedCadImages">
                    {{ cadExporting ? '正在生成 CAD 图片' : '下载 CAD 图片' }}
                  </a>
                </a-menu-item>
                <!-- <a-menu-item>
                <a @click="handleBatchEmergencyTypeEdit(selectedRowKeys)"
                  >设置工单紧急类型</a
                >
              </a-menu-item> -->
              </a-menu>
            </a-dropdown>
          </template>
          <div v-else></div>

          <!-- <c-upload
          name="file"
          :accept="'.xlsx'"
          :show-upload-list="false"
          :action="`${uploadUrl}orders/products/imports`"
          @change="handleFileChange"
        >
          <a-button
            class="ml-4"
            slot="content"
            type="primary"
            :loading="uploading"
            icon="import"
          >
            导入生产工单
          </a-button>
        </c-upload> -->
        </div>

        <!-- <a-button type="link" icon="download" @click="handleDownloadTemplate">
        点击下载生产工单导入模板
      </a-button> -->
      </div>
    </div>
    <a-table
      row-key="id"
      bordered
      class="plans-table"
      :row-selection="{
        selectedRowKeys: selectedRowKeys,
        onChange: onSelectChange,
      }"
      :columns="taskColumns"
      :data-source="taskList"
      :loading="loading"
      :pagination="pagination"
      :scroll="{ x: 2700, y: 'calc(100vh - 290px)' }"
      @change="handleTableChange"
    >
      <div
        slot="filterDropdown"
        slot-scope="{
          setSelectedKeys,
          selectedKeys,
          confirm,
          clearFilters,
          column,
        }"
        style="padding: 8px"
      >
        <template v-if="column.searchType === 'text'">
          <a-input
            v-ant-ref="(c) => (searchInput = c)"
            :placeholder="`搜索 ${column.title}`"
            :value="selectedKeys[0]"
            style="width: 188px; margin-bottom: 8px; display: block"
            @change="
              (e) => setSelectedKeys(e.target.value ? [e.target.value] : [])
            "
            @pressEnter="() => handleSearch(selectedKeys, confirm, column.key)"
          />
        </template>
        <div v-else-if="column.searchType === 'numberInput'">
          <a-input-number
            v-ant-ref="(c) => (searchInput = c)"
            :value="selectedKeys[0]"
            :placeholder="`搜索 ${column.title}`"
            :min="0"
            :max="999999"
            style="width: 188px; margin-bottom: 8px; display: block"
            @change="(e) => setSelectedKeys(e ? [e] : [])"
            @pressEnter="() => handleSearch(selectedKeys, confirm, column.key)"
          />
        </div>
        <div v-else-if="column.searchType === 'number'">
          <a-input-number
            v-model="selectedKeys[0]"
            size="small"
            placeholder="最小值"
            :min="0"
            :max="999999"
            style="width: 95px; margin-bottom: 8px; display: inline-block"
            @change="(e) => setSelectedKeys([e, selectedKeys[1]])"
            @pressEnter="() => handleSearch(selectedKeys, confirm, column.key)"
          />
          <span>-</span>
          <a-input-number
            v-model="selectedKeys[1]"
            size="small"
            placeholder="最大值"
            :min="selectedKeys[0]"
            :max="999999"
            style="width: 95px; margin-bottom: 8px; display: inline-block"
            @change="(e) => setSelectedKeys([selectedKeys[0], e])"
            @pressEnter="() => handleSearch(selectedKeys, confirm, column.key)"
          />
        </div>
        <div v-else-if="column.searchType === 'date'">
          <a-range-picker
            :show-time="{ format: 'HH:mm:ss' }"
            size="small"
            style="width: 330px; margin-bottom: 8px"
            :ranges="{
              今天: [$moment(), $moment()],
              本月: [$moment().startOf('month'), $moment().endOf('month')],
            }"
            @change="(dates, dateStrings) => setSelectedKeys([...dateStrings])"
          />
        </div>
        <a-button
          type="primary"
          icon="search"
          size="small"
          style="width: 90px; margin-right: 8px"
          @click="() => handleSearch(selectedKeys, confirm, column.key)"
          >搜索</a-button
        >
        <a-button
          size="small"
          style="width: 90px"
          @click="() => handleReset(selectedKeys, clearFilters, column.key)"
          >重置</a-button
        >
      </div>
      <a-icon
        slot="filterIcon"
        slot-scope="filtered"
        type="search"
        :style="{ color: filtered ? '#108ee9' : undefined }"
      />
      <!-- <div slot="code" slot-scope="text, record">
        <span class="text-blue-500" v-if="record.category === 'SUM'">
          {{ text }}
        </span>
        <span v-else>{{ text }}</span>
      </div> -->
      <div
        slot="type"
        slot-scope="text"
      >
        <template v-if="text === 0">
          <span>普通工单</span>
        </template>
        <template v-if="text === 1">
          <span>返修工单</span>
        </template>
        <template v-if="text === 2">
          <span>生产补投工单</span>
        </template>
        <template v-if="text === 3">
          <span>协作工单</span>
        </template>
        <template v-if="text === 4">
          <span>普通补投工单</span>
        </template>
        <template v-if="text === 5">
          <span>库存转入工单</span>
        </template>
      </div>
      <template slot="generateImgTitle">
        <div>
          <span>工单二维码</span>
          <!-- <a-dropdown>
            <span class="text-blue-500 ml-4" @click="(e) => e.preventDefault()">
              生成<a-icon type="down" />
            </span>
            <a-menu slot="overlay">
              <a-menu-item
                :disabled="loading"
                @click="generateAllImages(taskList)"
              >
                批量生成
              </a-menu-item>
              <a-menu-item @click="cancel"> 取消生成 </a-menu-item>
            </a-menu>
          </a-dropdown> -->
        </div>
      </template>
      <template
        slot="generateImg"
        slot-scope="text, record"
      >
        <div v-if="judgeHasOpr(record.category)">
          <a-button
            type="default"
            size="small"
            :block="false"
            @click="handleCreateTaskCard(record)"
          >
            打印{{ record.qrUseCount > 0 ? '完成' : '' }}
          </a-button>
        </div>
        <div v-else>
          <!-- 生成按钮 -->
          <a-button
            v-if="!record.imageGenerated"
            type="default"
            size="small"
            icon="picture"
            :block="false"
            :loading="record.generating"
            @click="handleGenerateImgBefore(record)"
          >
            生成图片
          </a-button>

          <!-- 图片预览和操作区域 -->
          <div
            v-else
            class="image-actions"
          >
            <!-- 操作按钮组 -->
            <a-button-group>
              <a-button
                size="small"
                type="default"
                @click="previewImage(record)"
                icon="eye"
              >
                预览
              </a-button>
              <!-- <a-button
                size="small"
                type="primary"
                @click="downloadImage(record)"
                icon="download"
              >
                下载
              </a-button> -->
              <a-button
                size="small"
                type="dashed"
                @click="beforeCopyImage(record)"
                icon="copy"
              >
                复制{{ record.qrUseCount > 0 ? '完成' : '' }}
              </a-button>
            </a-button-group>
          </div>
        </div>
      </template>
      <template
        slot="products"
        slot-scope="text, record"
      >
        <a-button
          v-if="judgeHasOpr(record.category)"
          type="default"
          size="small"
          :block="false"
          @click="handleTaskProductsModal(record)"
        >
          点击查看
        </a-button>
      </template>
      <span
        slot="emgType"
        slot-scope="text"
      >
        <a-tag
          v-if="text === 'NML'"
          color="volcano"
          >普通</a-tag
        >
        <a-tag
          v-if="text === 'MFN'"
          color="cyan"
          >必完件</a-tag
        >
        <a-tag
          v-if="text === 'UGC'"
          color="green"
          >紧急件</a-tag
        >
        <a-tag
          v-if="text === 'IUG'"
          color="purple"
          >插入急件</a-tag
        >
        <a-tag
          v-if="text === 'TUG'"
          color="blue"
          >转入急件</a-tag
        >
      </span>
      <span
        slot="category"
        slot-scope="text"
      >
        <a-tag :color="taskCardTypeColorMap[text]">{{
          taskCardTypeTextMap[text]
        }}</a-tag>
      </span>
      <div
        slot="status"
        slot-scope="text"
      >
        <template v-if="text === 0">
          <a-badge status="default" />准备中
        </template>
        <template v-if="text === 1">
          <a-badge status="default" />未开始
        </template>
        <template v-if="text === 2">
          <a-badge status="processing" />进行中
        </template>
        <template v-if="text === 3">
          <a-badge status="success" />已完成
        </template>
        <template v-if="text === 4">
          <a-badge status="error" />超期进行中
        </template>
        <template v-if="text === 5">
          <a-badge status="warning" />即将超期
        </template>
        <template v-if="text === 9">
          <a-badge status="warning" />暂停中
        </template>
        <template v-if="text === 8">
          <a-badge status="error" />已取消
        </template>
        <template v-if="text === 10">
          <a-badge status="error" />异常终止
        </template>
      </div>
      <div
        slot="deliveryStatus"
        slot-scope="text"
      >
        <a-tag
          v-if="text === 'GREEN'"
          color="green"
          >绿单</a-tag
        >
        <a-tag
          v-if="text === 'RED'"
          color="red"
          >红单</a-tag
        >
        <a-tag
          v-if="text === 'YELLOW'"
          color="orange"
          >黄单</a-tag
        >
        <a-tag
          v-if="text === 'BLACK'"
          color="black"
          >黑单</a-tag
        >
      </div>
      <div
        slot="actualUseMaterialList"
        slot-scope="text"
      >
        <a-tooltip>
          <template slot="title">
            <span
              v-for="(item, index) in text"
              :key="index"
            >
              {{ item.materialName }}({{ item.materialCode }})
              <span v-if="index < text.length - 1">,</span>
            </span>
          </template>
          <div class="table-ellis">
            <span
              v-for="(item, index) in text"
              :key="index"
              >{{ item.materialName }}({{ item.materialCode }}),</span
            >
          </div>
        </a-tooltip>
      </div>
      <div
        slot="operation"
        slot-scope="text, column"
        class="table-operation"
      >
        <a-dropdown>
          <a-menu
            slot="overlay"
            class="ant-dropdown-link"
          >
            <a-menu-item
              v-if="
                column.status === 0 ||
                column.status === 1 ||
                column.status === 2 ||
                column.status === 4 ||
                column.status === 5
              "
              @click="switchTaskStatus(column, 'c')"
              >取消工单</a-menu-item
            >
            <a-menu-item
              v-if="
                column.status === 2 ||
                column.status === 4 ||
                column.status === 5
              "
              @click="switchTaskStatus(column, 's')"
              >暂停工单</a-menu-item
            >
            <a-menu-item
              v-if="column.status === 9"
              @click="switchTaskStatus(column, 'r')"
              >恢复工单</a-menu-item
            >
            <a-menu-item
              v-if="
                column.status === 2 ||
                column.status === 4 ||
                column.status === 5
              "
              @click="handleDeleteTask(column)"
            >
              删除工单
            </a-menu-item>
            <a-menu-item
              v-if="judgeHasOpr(column.category)"
              @click="handleCreateTaskCard(column)"
            >
              打印工单
            </a-menu-item>
          </a-menu>
          <a class="operation-btn">
            操作&nbsp;
            <a-icon type="down" />
          </a>
        </a-dropdown>
      </div>
    </a-table>
    <el-dropdown
      class="export-task"
      @command="handleExportTaskCommand"
    >
      <el-button
        :loading="exportLoading"
        size="medium"
        type="primary"
        icon="el-icon-download"
        >导出工单</el-button
      >
      <el-dropdown-menu slot="dropdown">
        <el-dropdown-item command="FILTERED">导出当前筛选工单</el-dropdown-item>
      </el-dropdown-menu>
    </el-dropdown>
    <TaskEditModal
      :task-form="currentTask"
      v-if="visible"
    />
    <TaskBatchEdit
      v-if="batchTaskEditVisible"
      :task-form="currentTask"
      @cancel="handleTaskBatchModalCanceled"
      @confirm="handleTaskBatchModalConfirmed"
    />
    <TaskAssembleEdit
      v-if="assembleTaskEditVisible"
      :task-form="currentSelectTaskIds"
      @cancel="handleTaskAssembleModalCanceled"
      @confirm="handleTaskAssembleModalConfirmed"
    />
    <TaskEmergencyEditModal
      v-if="emergencyEditVisible"
      :ids="emergencyEditIds"
      :record="emergencyEditColumn"
      @confirm="handleTaskEmergencyTypeEditConfirm"
      @cancel="handleTaskEmergencyTypeEditCancel"
    />
    <TaskAssembleModal
      v-if="assembleTaskModalVisible"
      :ids="assembleIds"
      @cancel="handleTaskAssembleCancel"
      @confirm="handleTaskAssembleConfirm"
    />

    <!-- 图片预览模态框 -->
    <a-modal
      :visible="previewVisible"
      :title="previewTitle"
      :footer="null"
      :width="560"
      @cancel="closePreview"
      centered
    >
      <div class="preview-content">
        <img
          :src="previewImageUrl"
          class="preview-image w-[500px]"
          alt="图片预览"
        />
        <div class="mt-12 flex justify-end">
          <a-button
            type="primary"
            @click="downloadCurrentPreview"
            icon="download"
          >
            下载图片
          </a-button>
          <a-button
            class="ml-4"
            type="primary"
            @click="beforeCopyImage(previewRecord)"
            icon="copy"
          >
            复制
          </a-button>
        </div>
      </div>
    </a-modal>

    <!-- 隐藏的图片生成模板 -->
    <div
      ref="imageTemplate"
      class="image-template"
      :style="{ visibility: showTemplate ? 'visible' : 'hidden' }"
    >
      <div class="template-container">
        <div class="left-section">
          <div
            class="field-list"
            v-if="Object.keys(currentFields).length > 0"
          >
            <div
              class="flex flex-col justify-center text-base mb-2 border-b border-solid border-t-0 border-l-0 border-r-0"
            >
              <div
                class="flex justify-center text-[#333] text-[17px] font-bold"
              >
                {{ currentFields.client.name }}
              </div>
              <div class="flex justify-center">
                {{ currentFields.client.value }}
              </div>
            </div>
            <div class="flex">
              <div
                class="right-section justify-end"
                ref="qrcodeContainerLeft"
              />
              <div class="ml-4">
                <div
                  class="field-item"
                  v-for="field in currentFields.info"
                  :key="field.name"
                >
                  <div class="field-label">{{ field.name }}：</div>
                  <div class="field-value">{{ field.value }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div
          class="right-section"
          ref="qrcodeContainerRight"
        />
      </div>
    </div>
    <!-- <canvas ref="canvas500" style="vivisibility: hidden;" /> -->
    <!-- <canvas ref="canvas90" /> -->

    <OrderEdit
      v-if="editModalVisible"
      :modal-title="currentEditType"
      :order-form="currentOrder"
      @cancel="handleOrderModalCanceled"
      @confirm="handleOrderModalConfirmed"
    />
    <OrderNumEdit
      v-if="editNumVisible"
      :task="currentOrder"
      :on-confirm="handleNumModalConfirmed"
      @cancel="handleNumModalCanceled"
    />
    <TaskProducts
      v-if="taskProductsVisible"
      :task="currentTaskOrder"
      @cancel="handleTaskProductsCanceled"
      @confirm="handleTaskProductsConfirmed"
    />
  </div>
</template>
<script>
import html2canvas from 'html2canvas'
import QRCode from 'qrcode'
import dayjs from 'dayjs'
import { set } from 'lodash-es'

import { deepClone } from '@/utils'
import { downloadItem } from '@/utils/api.request'
import { CONSTANTS } from '@/utils/constants'
import { taskColumns } from './dataLogic'
import TaskEditModal from '@/components/TaskEditModal'
import TaskBatchEdit from '@/components/TaskBatchEdit'
import TaskAssembleEdit from '@/components/TaskAssembleEdit'
import TaskAssembleModal from '@/components/TaskAssembleModal'
import TaskEmergencyEditModal from '@/components/TaskEmergencyEditModal'
import {
  getTasks,
  deleteTask,
  exportTasks,
  switchTaskStatus,
  postTaskCopyAction,
} from '@/api/task'
import { getAllGroups } from '@/api/group'
import { downloadTemplate } from '@/common/templateDownload'
import OrderEdit from './OrderEdit'
import OrderNumEdit from './OrderNumEdit'
import TaskProducts from './taskProducts'
import {
  taskCardTypeColorMap,
  taskCardTypeTextMap,
  taskCardTypes,
} from '@/common/task'

export default {
  components: {
    OrderEdit,
    OrderNumEdit,
    TaskEditModal,
    TaskBatchEdit,
    TaskAssembleEdit,
    TaskAssembleModal,
    TaskEmergencyEditModal,
    TaskProducts,
  },
  data() {
    return {
      uploadUrl: process.env.VUE_APP_BASE_API,
      taskCardTypeColorMap,
      taskCardTypeTextMap,
      taskCardTypes,
      uploading: false,
      loading: false,
      taskList: [],
      taskColumns,
      selectedRowKeys: [],
      searchInput: null,
      exportIds: [],
      pagination: {
        total: 0,
        pageSize: 20,
        showSizeChanger: true,
        pageSizeOptions: ['20', '30', '40', '50'],
        showTotal: (total) => `共有 ${total} 条数据`,
        showQuickJumper: true,
      },
      currentTaskListQueryParams: {
        pageNum: 1,
        pageSize: 20,
        sort_by: null,
      },
      currentEditType: '更新',
      currentTask: {},
      currentSelectTaskIds: [],
      visible: false,
      batchTaskEditVisible: false,
      assembleTaskEditVisible: false,
      columnSearchParams: {},

      selectedRowPlanIds: [],
      exportLoading: false,
      emergencyEditVisible: false,
      emergencyEditIds: [],

      assembleIds: '',
      assembleTaskModalVisible: false,
      emergencyEditColumn: {},

      showTemplate: false,
      currentFields: {},
      currentQrData: '',
      currentRecord: null,
      generateImgCacheMap: new Map(),
      // 预览相关
      previewVisible: false,
      previewImageUrl: '',
      previewTitle: '',
      previewRecord: null,
      cadExporting: false,

      currentOrder: {},
      editModalVisible: false,

      editNumVisible: false,

      currentTaskOrder: {},
      taskProductsVisible: false,
      currentActiveKey: 'ALL',
      tableKey: 1,
    }
  },
  computed: {
    hasSelected() {
      return this.selectedRowKeys.length > 0
    },
  },
  created() {
    this.getTasks(this.currentTaskListQueryParams)
    this.getGroups()
  },
  methods: {
    judgeHasOpr(cate) {
      return !!cate && ['SUM', 'INV'].includes(cate)
    },
    handleTabChange(activeKey) {
      if (activeKey === 'ALL') {
        delete this.currentTaskListQueryParams['task.category']
      } else {
        this.currentTaskListQueryParams['task.category'] = 'eq:' + activeKey
      }
      this.selectedRowKeys = []
      this.selectedRowPlanIds = []
      this.currentTaskListQueryParams.pageNum = 1
      this.columnSearchParams = {}
      this.pagination.current = 1
      this.pagination.pageSize = 20
      this.getTasks(this.currentTaskListQueryParams)
      this.currentActiveKey = activeKey
      this.tableKey += 1
    },
    handleTaskProductsModal(record) {
      this.taskProductsVisible = true
      this.currentTaskOrder = record
    },
    handleTaskProductsCanceled() {
      this.taskProductsVisible = false
      this.currentTaskOrder = {}
    },
    handleTaskProductsConfirmed() {
      this.taskProductsVisible = false
      this.currentTaskOrder = {}
    },
    handleNumModalCanceled() {
      this.editNumVisible = false
    },
    async handleNumModalConfirmed(count) {
      this.editNumVisible = false

      set(this.currentOrder, 'productionNum', count)
      await this.generateImage(this.currentOrder)
    },
    handleDownloadTemplate() {
      downloadTemplate('orderProduct')
    },
    handleAddOrder() {
      this.currentEditType = '新增'
      this.currentOrder = {}
      this.editModalVisible = true
    },
    handleOrderModalConfirmed() {
      this.editModalVisible = false
      this.getTasksByParams()
    },
    handleOrderModalCanceled() {
      this.editModalVisible = false
    },
    handleFileChange(info) {
      this.uploading = true
      if (info.file.status === 'done') {
        if (info.file.response) {
          this.showImportResults(info.file.response)
        }
      } else if (info.file.status === 'error') {
        this.uploading = false
        this.$message.error(`批量导入失败，请稍后再试`)
      }
    },
    showImportResults(response) {
      const h = this.$createElement
      this.$info({
        title: '订单产品数据导入结果',
        width: 600,
        content: h('div', {}, [
          h('p', '导入数据总计：' + response.totalNum + '条;'),
          h('p', '非法数据条数：' + response.failedNum + ';'),
          h('p', '导入成功数据条数：' + response.successfulNum + ';'),
          h('p', '订单导入成功条数：' + response.orderSuccessNum + ';'),
          h('p', '产品导入成功条数：' + response.productSuccessNum + ';'),
          h('p', '非法数据信息:'),
          h('p', response.invalidMessages.join(' ||  ')),
        ]),
        onOk: () => {
          this.uploading = false
          this.$refs.pagination.currentChange(this.currentPageNum)
        },
      })
    },
    getGroups() {
      getAllGroups({
        withMembers: false,
      }).then((res) => {
        const filters = res.data.map((item) => {
          return {
            text: item.groupName,
            value: item.id,
          }
        })
        this.taskColumns.some((item) => {
          if (item.key === 'teamGroup.id') {
            item.filters = filters
            return true
          }
        })
      })
    },
    getTasks(data) {
      this.cancel()
      this.loading = true
      getTasks(data)
        .then((res) => {
          this.taskList = res.data.records.map((item) => {
            if (this.generateImgCacheMap.has(item.code)) {
              const { dataURL, blob, fileName } = this.generateImgCacheMap.get(
                item.code
              )
              set(item, 'imageGenerated', true)
              set(item, 'imageUrl', dataURL)
              set(item, 'imageBlob', blob)
              set(item, 'fileName', fileName)
            }

            return item
          })
          this.pagination.total = res.data.total
          // this.generateAllImages(this.taskList)
        })
        .finally(() => {
          this.loading = false
        })
    },
    // 根据当前分页数据和搜索条件查询数据
    getTasksByParams() {
      const cQueryParams = deepClone(this.currentTaskListQueryParams)
      const queryParams = Object.assign(cQueryParams, this.columnSearchParams)
      this.getTasks(queryParams)
    },
    onSelectChange(selectedRowKeys, selectedRows) {
      this.selectedRowKeys = selectedRowKeys
      this.selectedRowPlanIds = selectedRows.map((val) => {
        return val.planId
      })
    },
    handleTableChange(pagination, filters, sorter) {
      const filtersKeys = Object.keys(filters)

      if (filtersKeys.includes('type')) {
        delete this.columnSearchParams['type']
        if (filters['type'].length > 0) {
          this.columnSearchParams['task.type'] = `in:${filters[
            'type'
          ].toString()}`
        } else {
          delete this.columnSearchParams['task.type']
        }
      }

      if (filtersKeys.includes('emgType')) {
        delete this.columnSearchParams['emgType']
        if (filters['emgType'].length > 0) {
          this.columnSearchParams['emgType'] = `in:${filters[
            'emgType'
          ].toString()}`
        } else {
          delete this.columnSearchParams['emgType']
        }
      }
      if (filtersKeys.includes('code')) {
        delete this.columnSearchParams['code']
        if (filters['code'].length > 0) {
          this.columnSearchParams['task.code'] = `like:${filters[
            'code'
          ].toString()}`
        } else {
          delete this.columnSearchParams['task.code']
        }
      }

      if (filtersKeys.includes('status')) {
        delete this.columnSearchParams['status']
        if (filters['status'].length > 0) {
          this.columnSearchParams['task.status'] = `in:${filters[
            'status'
          ].toString()}`
        } else {
          delete this.columnSearchParams['task.status']
        }
      }
      if (filtersKeys.includes('category')) {
        delete this.columnSearchParams['category']
        if (filters['category'].length > 0) {
          this.columnSearchParams['task.category'] = `in:${filters[
            'category'
          ].toString()}`
        } else {
          delete this.columnSearchParams['task.category']
        }
      }

      if (filtersKeys.includes('deliveryStatus')) {
        delete this.columnSearchParams['deliveryStatus']
        if (filters['deliveryStatus'].length > 0) {
          this.columnSearchParams['task.deliveryStatus'] = `in:${filters[
            'deliveryStatus'
          ].toString()}`
        } else {
          delete this.columnSearchParams['task.deliveryStatus']
        }
      }

      if (filtersKeys.includes('teamGroup.id')) {
        delete this.columnSearchParams['teamGroup.id']
        if (filters['teamGroup.id'].length > 0) {
          this.columnSearchParams['teamGroup.id'] = `in:${filters[
            'teamGroup.id'
          ].toString()}`
        } else {
          delete this.columnSearchParams['teamGroup.id']
        }
      }

      if (sorter.order) {
        const sortType = sorter.order === 'ascend' ? '+' : '-'
        if (sorter.columnKey === 'code') {
          sorter.columnKey = 'task.code'
        }
        this.currentTaskListQueryParams.sort_by = sortType + sorter.columnKey
      } else {
        this.currentTaskListQueryParams.sort_by = null
      }

      this.pagination = pagination
      this.currentTaskListQueryParams.pageNum = pagination.current
      this.currentTaskListQueryParams.pageSize = pagination.pageSize
      this.getTasksByParams()
    },
    // 删除工单
    handleDeleteTask(column) {
      const taskCode = column.code ? column.code : ''
      this.$confirm({
        content: '确认删除工单' + taskCode + '?',
        onOk: () => {
          deleteTask(column.id).then((res) => {
            if (res) {
              this.$message.success('删除工单成功！')
              this.getTasksByParams()
            }
          })
        },
      })
    },
    // 改变工单状态
    switchTaskStatus(row, statusCode) {
      let actionName = ''
      if (statusCode === 'c') {
        actionName = '取消'
      } else if (statusCode === 's') {
        actionName = '暂停'
      } else if (statusCode === 'r') {
        actionName = '恢复'
      }
      this.$confirm({
        content: '确认' + actionName + '工单' + row.code + '?',
        onOk: () => {
          switchTaskStatus(row.id, statusCode).then(() => {
            this.$message.success(actionName + '工单成功！')
            this.getTasksByParams()
          })
        },
      })
    },
    // 更新工单
    handleUpdateTask(column) {
      this.currentTask = deepClone(column)
      this.visible = true
    },
    batchTask(column) {
      this.batchTaskEditVisible = true
      this.currentTask = deepClone(column)
    },
    assembleTask(column) {
      this.currentSelectTaskIds.push(column.id)
      this.assembleTaskEditVisible = true
    },
    handleTaskBatchModalCanceled() {
      this.batchTaskEditVisible = false
    },
    handleTaskBatchModalConfirmed() {
      this.batchTaskEditVisible = false
      this.$message.success('工单分批成功')
      this.getTasksByParams()
    },
    handleTaskAssembleModalCanceled() {
      this.assembleTaskEditVisible = false
    },
    handleTaskAssembleModalConfirmed() {
      this.assembleTaskEditVisible = false
      this.$message.success('工单装配成功')
      this.getTasksByParams()
    },
    handleEditCancel() {
      this.currentTask = {}
      this.visible = false
    },
    handleSearch(selectedKeys, confirm, dataIndex) {
      confirm()
      this.columnSearchParams[dataIndex] = ''
      console.log(selectedKeys)
      if (selectedKeys && selectedKeys.length === 1) {
        if (selectedKeys[0].trim() !== '') {
          this.columnSearchParams[dataIndex] = 'like:' + selectedKeys[0].trim()
        }
      }
      if (selectedKeys && selectedKeys.length === 2) {
        if (selectedKeys[0] || selectedKeys[1]) {
          if (dataIndex === 'stayTime') {
            const minTime = this.$moment()
              .subtract(selectedKeys[1], 'hours')
              .format('YYYY-MM-DD HH:mm:ss')
            const maxTime = this.$moment()
              .subtract(selectedKeys[0], 'hours')
              .format('YYYY-MM-DD HH:mm:ss')
            this.columnSearchParams['latestOperationTime'] =
              'btn:' + minTime + ',' + maxTime
          } else {
            this.columnSearchParams[dataIndex] =
              'btn:' + selectedKeys.toString()
          }
        }
      }
    },
    handleReset(selectedKeys, clearFilters, dataIndex) {
      clearFilters()
      if (dataIndex === 'stayTime') {
        this.columnSearchParams['latestOperationTime'] = null
      }
      this.columnSearchParams[dataIndex] = null
    },
    getExportIdsFromSelection() {
      this.exportIds = this.selectedRowKeys
    },
    getExportIdsFromPage() {
      this.exportIds = this.taskList.map((item) => {
        return item.id
      })
    },
    handleExportTaskCommand(command) {
      const cQueryParams = deepClone(this.currentTaskListQueryParams)
      const queryParams = Object.assign(cQueryParams, this.columnSearchParams)
      switch (command) {
        case 'FILTERED':
          this.exportLoading = true
          exportTasks(queryParams)
            .then((res) => {
              this.downloadTask(res)
              this.exportLoading = false
            })
            .catch((e) => {
              this.$message.error('导出失败，请稍后重试!')
            })
            .finally(() => {
              this.exportLoading = false
            })

          break
        default:
          break
      }
    },
    downloadTask(blobData) {
      const currentDate = +Date.now()
      downloadItem(
        blobData,
        'application/vnd.ms-excel',
        CONSTANTS.TASK_FILE_NAME + currentDate + CONSTANTS.EXPORT_FILE_SUFFIX
      )
    },
    handleCreateTaskCard(record) {
      const cb = () => {
        const newPage = this.$router.resolve({
          path: '/task-card',
          query: {
            ids: record.id.toString(),
          },
        })
        window.open(newPage.href, '_blank')

        this.$confirm({
          content: '工单是否打印完成?',
          onOk: () => {
            this.getTasksByParams()
          },
        })
      }
      if (record.qrUseCount > 0) {
        this.$confirm({
          title: `工单 ${record.code} 已完成打印，是否继续打印`,
          onOk: () => {
            cb()
          },
          onCancel() {
            console.log('Cancel')
          },
        })
      } else {
        cb()
      }
    },
    handleCreateCraftCard(selectedRowKeys) {
      const newPage = this.$router.resolve({
        path: '/craft-card',
        query: {
          ids: selectedRowKeys.toString(),
        },
      })
      window.open(newPage.href, '_blank')
    },
    handleCheckCertificate(id) {
      const routeData = this.$router.resolve({
        name: 'certificate',
        query: {
          id: id,
        },
      })
      window.open(routeData.href, '_blank')
    },
    handleCreatePlanCard() {
      const newPage = this.$router.resolve({
        path: '/task-card',
        query: {
          ids: this.selectedRowKeys.toString(),
        },
      })
      window.open(newPage.href, '_blank')

      this.$confirm({
        content: '工单是否打印完成?',
        onOk: () => {
          this.getTasksByParams()
        },
      })
    },
    handleTaskEmergencyTypeEdit(column) {
      this.emergencyEditVisible = true
      this.emergencyEditIds = [column.id]
      this.emergencyEditColumn = column
    },
    handleBatchEmergencyTypeEdit(ids) {
      this.emergencyEditVisible = true
      this.emergencyEditIds = ids
      this.emergencyEditColumn = {}
    },
    handleTaskEmergencyTypeEditConfirm() {
      this.emergencyEditVisible = false
      this.getTasksByParams()
    },
    handleTaskEmergencyTypeEditCancel() {
      this.emergencyEditVisible = false
    },
    handleTaskAssemble(ids) {
      this.assembleIds = ids
      this.assembleTaskModalVisible = true
    },
    handleTaskAssembleConfirm() {
      this.assembleTaskModalVisible = false
      this.getTasksByParams()
    },
    handleTaskAssembleCancel() {
      this.assembleTaskModalVisible = false
    },

    handleGenerateImgBefore(record) {
      this.currentOrder = record
      // if (record.status === 3) {
      this.generateImage(this.currentOrder)
      // } else {
      //   this.editNumVisible = true
      // }
    },
    /**
     * 延时函数
     */
    delay(ms) {
      return new Promise((resolve) => setTimeout(resolve, ms))
    },

    /**
     * 生成图片
     */
    async generateImage(record, { preview = true } = {}) {
      try {
        if (record.imageGenerated && record.imageBlob) {
          if (preview) {
            this.previewImage(record)
          }
          return record
        }

        this.$set(record, 'generating', true)
        this.currentRecord = record

        // 准备数据
        this.prepareImageData(record)

        // 显示模板并生成二维码
        this.showTemplate = true
        await this.generateQRCode()

        // 等待DOM更新后生成图片
        await this.$nextTick()
        // await this.delay(200)

        // let originalCanvas = null
        // let canvas500 = null
        // let canvas90 = null

        // 2. 一次高分辨率渲染
        // originalCanvas = await html2canvas(this.$refs.imageTemplate, {
        //   scale: window.devicePixelRatio * 2,
        //   // scale: 5,
        //   useCORS: true,
        //   backgroundColor: '#fff',
        //   logging: false,
        // })

        // // 3. 500x250 预览
        // // canvas500 = document.createElement('canvas')
        // this.$refs.canvas500.width = 500
        // this.$refs.canvas500.height = 250
        // this.$refs.canvas500
        //   .getContext('2d')
        //   .drawImage(originalCanvas, 0, 0, 500, 250)
        // const dataURL = this.$refs.canvas500.toDataURL('image/png') // 给 Vue 预览绑定

        // // 4. 90x50 用于复制
        // // canvas90 = document.createElement('canvas')
        // this.$refs.canvas90.width = 90
        // this.$refs.canvas90.height = 50

        // const canvas90Ctx = this.$refs.canvas90.getContext('2d')
        // // 使用高质量图像缩放
        // canvas90Ctx.imageSmoothingEnabled = true
        // canvas90Ctx.imageSmoothingQuality = 'high'
        // canvas90Ctx.drawImage(originalCanvas, 0, 0, 90, 50)

        // 使用html2canvas生成图片
        const canvas = await html2canvas(this.$refs.imageTemplate, {
          backgroundColor: '#ffffff',
          scale: 1,
          useCORS: true,
          allowTaint: false,
          width: 500,
          height: 250,
        })
        // const miniCanvas = await html2canvas(this.$refs.imageTemplate, {
        //   backgroundColor: '#ffffff',
        //   scale: 2,
        //   useCORS: true,
        //   allowTaint: false,
        //   width: 90,
        //   height: 50,
        // })

        // // 将canvas转换为blob和dataURL
        const dataURL = canvas.toDataURL('image/png', 1.0)
        const blob = await this.dataURLToBlob(dataURL)
        // const miniDataURL = miniCanvas.toDataURL('image/png', 1.0)
        // const miniBlob = await this.dataURLToBlob(miniDataURL)
        // const miniBlob = await new Promise((resolve) =>
        //   // miniCanvas.toBlob(resolve, 'image/png')
        //   this.$refs.canvas90.toBlob(resolve, 'image/png')
        // )

        // console.log(777, dataURL, blob, miniBlob)
        // 更新记录数据
        const fileName = `工单信息_${record.code}_${Date.now()}.png`
        this.$set(record, 'imageGenerated', true)
        this.$set(record, 'imageUrl', dataURL)
        this.$set(record, 'imageBlob', blob)
        // this.$set(record, 'miniImageBlob', miniBlob)
        this.$set(record, 'fileName', fileName)

        this.generateImgCacheMap.set(record.code, {
          dataURL,
          blob,
          fileName,
        })
        if (preview) {
          this.previewImage(record)
        }
        return record
        // this.$message.success('图片生成成功！')
      } catch (error) {
        console.error('生成图片失败:', error)
        this.$message.error('生成图片失败，请重试')
        throw error
      } finally {
        // this.showTemplate = false
        this.$set(record, 'generating', false)
        this.clearQRCode()
      }
    },
    // 取消生成
    cancel() {
      this.isCancelled = true
    },
    /**
     * 批量生成所有图片（可选功能）
     */
    async generateAllImages(arr) {
      this.isCancelled = false // 重置取消状态

      for (const item of arr) {
        // 检查是否已取消
        if (this.isCancelled) {
          console.log('Generation cancelled')
          return // 直接退出循环
        }

        await this.generateImage(item, { preview: false })
        await this.delay(300) // 避免过快连续操作
      }
    },
    /**
     * 下载所选工单的 CAD 图片包。
     * 浏览器无法将多个文件直接写入指定目录，因此输出为 ZIP 文件。
     */
    async downloadSelectedCadImages() {
      if (this.cadExporting) {
        return
      }

      const selectedIds = new Set(this.selectedRowKeys)
      const records = this.taskList.filter((item) => selectedIds.has(item.id))

      if (records.length === 0) {
        this.$message.warning('请先选择需要导出的工单')
        return
      }

      const unsupportedRecords = records.filter((record) =>
        this.judgeHasOpr(record.category)
      )
      const exportRecords = records.filter(
        (record) => !this.judgeHasOpr(record.category)
      )

      if (exportRecords.length === 0) {
        this.$message.warning('所选工单不支持生成 CAD 图片')
        return
      }

      const fileNames = exportRecords.map((record) =>
        this.getCadImageFileName(record.code)
      )
      if (new Set(fileNames).size !== fileNames.length) {
        this.$message.error('所选工单的图片文件名重复，无法导出')
        return
      }

      this.cadExporting = true
      try {
        const files = []
        for (const record of exportRecords) {
          await this.generateImage(record, { preview: false })
          files.push({
            name: this.getCadImageFileName(record.code),
            blob: record.imageBlob,
          })
        }

        const zipBlob = await this.createStoredZip(files)
        this.downloadBlob(
          zipBlob,
          `CAD工单图片_${dayjs().format('YYYYMMDD_HHmmss')}.zip`
        )
        const skippedMessage =
          unsupportedRecords.length > 0
            ? `，已跳过 ${unsupportedRecords.length} 条不支持的工单`
            : ''
        this.$message.success(
          `已导出 ${files.length} 张 CAD 图片${skippedMessage}`
        )
      } catch (error) {
        console.error('导出 CAD 图片失败:', error)
        this.$message.error('导出 CAD 图片失败，请重试')
      } finally {
        this.cadExporting = false
      }
    },
    /**
     * 生成与 AutoCAD 插件一致的文件名。
     * Windows 文件名不能包含 \ / : * ? " < > |，使用下划线替换。
     */
    getCadImageFileName(code) {
      const safeCode = String(code || '')
        .trim()
        .replace(/[\\/:*?"<>|]/g, '_')
      return `${safeCode || '未命名工单'}.png`
    },
    /**
     * 创建仅存储 PNG 文件的 ZIP。PNG 已压缩，无需再引入额外压缩依赖。
     */
    async createStoredZip(files) {
      const encoder = new TextEncoder()
      const localFileParts = []
      const centralDirectoryParts = []
      let offset = 0

      for (const file of files) {
        const nameBytes = encoder.encode(file.name)
        const data = new Uint8Array(await file.blob.arrayBuffer())
        const crc = this.crc32(data)
        const { dosDate, dosTime } = this.getDosDateTime(new Date())

        const localHeader = new Uint8Array(30 + nameBytes.length)
        const localView = new DataView(localHeader.buffer)
        localView.setUint32(0, 0x04034b50, true)
        localView.setUint16(4, 20, true)
        localView.setUint16(6, 0x0800, true)
        localView.setUint16(8, 0, true)
        localView.setUint16(10, dosTime, true)
        localView.setUint16(12, dosDate, true)
        localView.setUint32(14, crc, true)
        localView.setUint32(18, data.length, true)
        localView.setUint32(22, data.length, true)
        localView.setUint16(26, nameBytes.length, true)
        localView.setUint16(28, 0, true)
        localHeader.set(nameBytes, 30)

        const centralHeader = new Uint8Array(46 + nameBytes.length)
        const centralView = new DataView(centralHeader.buffer)
        centralView.setUint32(0, 0x02014b50, true)
        centralView.setUint16(4, 20, true)
        centralView.setUint16(6, 20, true)
        centralView.setUint16(8, 0x0800, true)
        centralView.setUint16(10, 0, true)
        centralView.setUint16(12, dosTime, true)
        centralView.setUint16(14, dosDate, true)
        centralView.setUint32(16, crc, true)
        centralView.setUint32(20, data.length, true)
        centralView.setUint32(24, data.length, true)
        centralView.setUint16(28, nameBytes.length, true)
        centralView.setUint16(30, 0, true)
        centralView.setUint16(32, 0, true)
        centralView.setUint16(34, 0, true)
        centralView.setUint16(36, 0, true)
        centralView.setUint32(38, 0, true)
        centralView.setUint32(42, offset, true)
        centralHeader.set(nameBytes, 46)

        localFileParts.push(localHeader, data)
        centralDirectoryParts.push(centralHeader)
        offset += localHeader.length + data.length
      }

      const centralDirectorySize = centralDirectoryParts.reduce(
        (size, part) => size + part.length,
        0
      )
      const endOfCentralDirectory = new Uint8Array(22)
      const endView = new DataView(endOfCentralDirectory.buffer)
      endView.setUint32(0, 0x06054b50, true)
      endView.setUint16(4, 0, true)
      endView.setUint16(6, 0, true)
      endView.setUint16(8, files.length, true)
      endView.setUint16(10, files.length, true)
      endView.setUint32(12, centralDirectorySize, true)
      endView.setUint32(16, offset, true)
      endView.setUint16(20, 0, true)

      return new Blob(
        [...localFileParts, ...centralDirectoryParts, endOfCentralDirectory],
        { type: 'application/zip' }
      )
    },
    getDosDateTime(date) {
      const year = Math.max(date.getFullYear(), 1980)
      return {
        dosDate:
          ((year - 1980) << 9) |
          ((date.getMonth() + 1) << 5) |
          date.getDate(),
        dosTime:
          (date.getHours() << 11) |
          (date.getMinutes() << 5) |
          Math.floor(date.getSeconds() / 2),
      }
    },
    crc32(data) {
      let crc = 0xffffffff
      for (let index = 0; index < data.length; index += 1) {
        crc ^= data[index]
        for (let bit = 0; bit < 8; bit += 1) {
          crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0)
        }
      }
      return (crc ^ 0xffffffff) >>> 0
    },
    beforeCopyImage(record) {
      if (record.qrUseCount > 0 || record.hasCopied) {
        this.$confirm({
          title: `工单 ${record.code} 已完成复制，是否继续复制`,
          onOk: () => {
            this.copyImage(record)
          },
          onCancel() {
            console.log('Cancel')
          },
        })
      } else {
        this.copyImage(record)
      }
    },
    async copyImage(record) {
      try {
        await postTaskCopyAction(record.id)
        await navigator.clipboard.write([
          // eslint-disable-next-line no-undef
          new ClipboardItem({
            // 原图
            [record.imageBlob.type]: record.imageBlob,
            // mini 版图片
            // [record.miniImageBlob.type]: record.miniImageBlob,
          }),
        ])
        this.$set(record, 'hasCopied', true)
        this.$message.success('复制成功！')
        this.getTasks(this.currentTaskListQueryParams)
        this.previewVisible = false
      } catch (e) {
        console.error(e)
        this.$message.error('图片复制失败，请重试')
      }
    },

    /**
     * 预览图片
     */
    previewImage(record) {
      this.previewRecord = record
      this.previewImageUrl = record.imageUrl
      this.previewTitle = `工单二维码: ${record.code}`
      this.previewVisible = true
    },

    /**
     * 关闭预览
     */
    closePreview() {
      this.previewVisible = false
      this.previewImageUrl = ''
      this.previewTitle = ''
      this.previewRecord = null
    },

    /**
     * 下载图片
     */
    downloadImage(record) {
      if (record.imageBlob && record.fileName) {
        this.downloadBlob(record.imageBlob, record.fileName)
        this.$message.success('图片下载成功！')
      } else {
        this.$message.error('图片数据不存在，请重新生成')
      }
    },

    /**
     * 下载当前预览的图片
     */
    downloadCurrentPreview() {
      if (this.previewRecord) {
        this.downloadImage(this.previewRecord)
        this.closePreview()
      }
    },

    /**
     * 准备图片数据
     */
    prepareImageData(record) {
      this.currentFields = {
        client: { name: '客户编号', value: record.clientCode },
        info: [
          {
            name: '下单时间',
            value: dayjs(record.signTime).format('YYYY-MM-DD'),
          },
          {
            name: '交货时间',
            value: dayjs(record.deadline).format('YYYY-MM-DD'),
          },
          { name: '项目编号', value: record.clientProjectCode },
          { name: '工单编号', value: record.drawingSeq },
        ],
        count: { name: '产品数量', value: record.productionNum },
      }

      this.currentQrData = record.code
    },

    /**
     * 生成二维码
     */
    async generateQRCode() {
      try {
        this.clearQRCode()

        const qrCanvasLeft = document.createElement('canvas')
        await QRCode.toCanvas(qrCanvasLeft, this.currentQrData, {
          width: 100,
          height: 100,
          margin: 2,
          color: {
            dark: '#000000',
            light: '#FFFFFF',
          },
          errorCorrectionLevel: 'M',
        })
        const qrCanvasRight = document.createElement('canvas')
        await QRCode.toCanvas(qrCanvasRight, this.currentQrData, {
          width: 100,
          height: 100,
          margin: 2,
          color: {
            dark: '#000000',
            light: '#FFFFFF',
          },
          errorCorrectionLevel: 'M',
        })

        this.$refs.qrcodeContainerLeft.appendChild(qrCanvasLeft)
        this.$refs.qrcodeContainerRight.appendChild(qrCanvasRight)
      } catch (error) {
        console.error('生成二维码失败:', error)
        throw error
      }
    },

    /**
     * 清理二维码容器
     */
    clearQRCode() {
      if (this.$refs.qrcodeContainerLeft) {
        this.$refs.qrcodeContainerLeft.innerHTML = ''
      }
      if (this.$refs.qrcodeContainerRight) {
        this.$refs.qrcodeContainerRight.innerHTML = ''
      }
    },

    /**
     * DataURL转Blob
     */
    dataURLToBlob(dataURL) {
      return new Promise((resolve) => {
        const arr = dataURL.split(',')
        const mime = arr[0].match(/:(.*?);/)[1]
        const bstr = atob(arr[1])
        let n = bstr.length
        const u8arr = new Uint8Array(n)

        while (n--) {
          u8arr[n] = bstr.charCodeAt(n)
        }

        resolve(new Blob([u8arr], { type: mime }))
      })
    },

    /**
     * 下载Blob文件
     */
    downloadBlob(blob, fileName) {
      const link = document.createElement('a')
      const url = URL.createObjectURL(blob)

      link.href = url
      link.download = fileName
      document.body.appendChild(link)
      link.click()

      document.body.removeChild(link)
      URL.revokeObjectURL(url)
    },
  },
}
</script>
<style lang="scss" scoped>
.app-container {
  padding: 20px 20px 0;
  .selected-wrapper {
    height: 25px;
    line-height: 25px;
  }
  .batch-operate-link {
    padding-left: 20px;
    cursor: pointer;
    color: #409eff;
  }

  .operation-btn {
    cursor: pointer;
    color: #409eff;
  }

  .export-task {
    float: right;
    top: -50px;
    right: 20px;
  }
}
/deep/ .ant-table-pagination,
.ant-pagination {
  float: left;
}
/deep/ .ant-table-fixed-header .ant-table-scroll .ant-table-header {
  overflow: hidden;
}
.table-ellis {
  overflow: hidden; //超出的文本隐藏
  text-overflow: ellipsis; //溢出用省略号显示
  white-space: nowrap; //溢出不换行
}

/* 图片模板样式 */
.image-template {
  position: fixed;
  top: -2000px;
  left: -2000px;
  z-index: -1;
  background: #ffffff;
}

.template-container {
  width: 500px;
  display: flex;
  background: #ffffff;
  border: 1px solid #e8e8e8;
}

/* 左侧内容区域 */
.left-section {
  width: 400px;
  padding: 10px 0;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.title {
  margin: 0 0 10px 0;
  font-size: 24px;
  font-weight: bold;
  color: #1890ff;
  text-align: center;
  border-bottom: 2px solid #1890ff;
}

.field-list {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.field-item {
  margin-bottom: 8px;
  font-size: 17px;
  line-height: 1.5;
  display: flex;
  align-items: center;
}

.field-label {
  font-weight: bold;
  color: #333333;
  width: 86px;
}

.field-value {
  color: #666666;
  border-radius: 4px;
  flex: 1;
}

/* 右侧二维码区域 */
.right-section {
  width: 100px;
  background: #fafafa;
  display: flex;
  flex-direction: column;
}

/* 表格内按钮样式优化 */
.ant-btn-sm {
  font-size: 12px;
}

/* 图片操作区域 */
.image-actions {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

/* 缩略图容器 */
.thumbnail-container {
  position: relative;
  width: 60px;
  height: 40px;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.3s;
}

.thumbnail-container:hover {
  border-color: #40a9ff;
  box-shadow: 0 2px 8px rgba(24, 144, 255, 0.2);
}

.thumbnail-container:hover .image-overlay {
  opacity: 1;
}

.thumbnail {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 0.3s;
}

.image-overlay .anticon {
  color: white;
  font-size: 16px;
}
</style>
