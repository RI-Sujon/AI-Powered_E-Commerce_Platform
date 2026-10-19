const OrderService = {
    baseUrl: `${window.API_BASE_URL}/api/order`,

    async checkout() {
        const response = await $.ajax({
            url: `${this.baseUrl}/checkout`,
            method: 'POST',
            contentType: 'application/json',
            headers: ServiceUtils.getHeaders()
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Checkout failed');
    },

    async getOrderHistory(page, pageSize, status) {
        page = page || 1;
        pageSize = pageSize || 5;
        const data = { page, pageSize };
        if (status) data.status = status;
        const response = await $.ajax({
            url: this.baseUrl,
            method: 'GET',
            headers: ServiceUtils.getHeaders(),
            data
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Failed to load orders');
    },

    async getOrderById(id) {
        const response = await $.ajax({
            url: `${this.baseUrl}/${id}`,
            method: 'GET',
            headers: ServiceUtils.getHeaders()
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Failed to load order');
    },

    async updateOrderStatus(orderId, status) {
        const response = await $.ajax({
            url: `${this.baseUrl}/${orderId}/status`,
            method: 'PATCH',
            contentType: 'application/json',
            headers: ServiceUtils.getHeaders(),
            data: JSON.stringify({ status })
        });
        if (response.isSuccess) return true;
        throw new Error(response.errorMessage || 'Failed to update status');
    },

    async cancelOrder(orderId) {
        const response = await $.ajax({
            url: `${this.baseUrl}/${orderId}/cancel`,
            method: 'POST',
            headers: ServiceUtils.getHeaders()
        });
        if (response.isSuccess) return true;
        throw new Error(response.errorMessage || 'Failed to cancel order');
    }
};

window.OrderService = OrderService;
