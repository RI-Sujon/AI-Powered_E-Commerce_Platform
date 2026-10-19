const AuthService = {
    baseUrl: `${window.API_BASE_URL}/api/auth`,

    async login(email, password) {
        const response = await $.ajax({
            url: `${this.baseUrl}/login`,
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ email, password })
        });
        if (response.isSuccess) {
            Common.saveToken(response.data.token);
            return response.data;
        }
        throw new Error(response.errorMessage || 'Login failed');
    },

    async register(email, password, fullName) {
        const response = await $.ajax({
            url: `${this.baseUrl}/register`,
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ email, password, fullName })
        });
        if (response.isSuccess) {
            Common.saveToken(response.data.token);
            return response.data;
        }
        throw new Error(response.errorMessage || 'Registration failed');
    }
};

window.AuthService = AuthService;
