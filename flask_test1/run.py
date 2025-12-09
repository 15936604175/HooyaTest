from app import create_app

# 创建应用实例
app = create_app()

if __name__ == '__main__':
    # 启动应用，监听所有地址，端口5000
    app.run(host='0.0.0.0', port=5000, debug=True)