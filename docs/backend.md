# Vue3 + Flask 登录与图片展示系统后端开发文档

## 一、项目结构

```
flask_test1/
├── app/            # Flask 应用
│   ├── api/         # API 路由
│   │   ├── auth.py     # 认证相关 API
│   │   └── images.py   # 图片管理 API
│   ├── models/      # 数据库模型
│   │   └── models.py   # 定义数据模型
│   ├── __init__.py  # 应用初始化
│   └── config.py    # 配置文件
├── uploads/         # 上传文件存储目录
├── .env             # 环境变量配置
├── requirements.txt # 项目依赖
└── run.py           # 启动文件
```

## 二、核心模块

### 1. 应用初始化（__init__.py）

**功能**：创建 Flask 应用实例，配置数据库和 CORS，注册蓝图。

**核心代码**：
```python
def create_app():
    # 创建Flask应用实例
    app = Flask(__name__)
    
    # 加载配置
    app.config.from_object(Config)
    
    # 初始化数据库
    db.init_app(app)
    
    # 配置CORS
    CORS(app, resources={r"/*": {"origins": "*"}})
    
    # 注册蓝图
    from .api.auth import auth_bp
    from .api.images import images_bp
    
    app.register_blueprint(auth_bp, url_prefix='/api')
    app.register_blueprint(images_bp, url_prefix='/api/images')
    
    # 创建数据库表
    with app.app_context():
        db.create_all()
    
    return app
```

### 2. 配置文件（config.py）

**功能**：管理应用配置，包括数据库、JWT、文件上传等配置。

**核心代码**：
```python
class Config:
    # 应用配置
    SECRET_KEY = os.environ.get('SECRET_KEY') or 'your-secret-key'
    
    # 数据库配置
    DB_HOST = os.environ.get('DB_HOST') or 'localhost'
    DB_PORT = int(os.environ.get('DB_PORT') or 3306)
    DB_USER = os.environ.get('DB_USER') or 'root'
    DB_PASSWORD = os.environ.get('DB_PASSWORD') or 'password'
    DB_NAME = os.environ.get('DB_NAME') or 'vue_flask_db'
    
    # SQLAlchemy配置
    SQLALCHEMY_DATABASE_URI = f'mysql+pymysql://{DB_USER}:{DB_PASSWORD}@{DB_HOST}:{DB_PORT}/{DB_NAME}?charset=utf8mb4'
    SQLALCHEMY_TRACK_MODIFICATIONS = False
    
    # JWT配置
    JWT_SECRET_KEY = os.environ.get('JWT_SECRET_KEY') or 'your-jwt-secret-key'
    JWT_EXPIRATION_DELTA = 3600
    
    # 文件上传配置
    UPLOAD_FOLDER = os.path.join(os.getcwd(), 'uploads')
    ALLOWED_EXTENSIONS = {'png', 'jpg', 'jpeg', 'gif'}
    MAX_CONTENT_LENGTH = 16 * 1024 * 1024
```

### 3. 数据库模型（models.py）

**功能**：定义用户和图片数据模型。

**核心代码**：
```python
class User(db.Model):
    """用户模型"""
    __tablename__ = 'users'
    
    id = db.Column(db.Integer, primary_key=True, autoincrement=True)
    username = db.Column(db.String(50), unique=True, nullable=False)
    password = db.Column(db.String(100), nullable=False)
    created_at = db.Column(db.DateTime, default=datetime.utcnow)
    
    # 关系定义
    images = db.relationship('Image', backref='user', lazy=True)

class Image(db.Model):
    """图片模型"""
    __tablename__ = 'images'
    
    id = db.Column(db.Integer, primary_key=True, autoincrement=True)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    filename = db.Column(db.String(255), nullable=False)
    filepath = db.Column(db.String(255), nullable=False)
    created_at = db.Column(db.DateTime, default=datetime.utcnow)
```

## 三、API 接口

### 1. 认证 API（auth.py）

#### 1.1 注册接口
- **URL**：`/api/register`
- **方法**：`POST`
- **请求体**：`{"username": "string", "password": "string"}`
- **响应**：`{"message": "注册成功"}`

#### 1.2 登录接口
- **URL**：`/api/login`
- **方法**：`POST`
- **请求体**：`{"username": "string", "password": "string"}`
- **响应**：`{"token": "string", "message": "登录成功"}`

#### 1.3 登出接口
- **URL**：`/api/logout`
- **方法**：`POST`
- **响应**：`{"message": "登出成功"}`

#### 1.4 刷新 Token 接口
- **URL**：`/api/refresh`
- **方法**：`POST`
- **请求头**：`Authorization: Bearer <token>`
- **响应**：`{"token": "string", "message": "token刷新成功"}`

### 2. 图片管理 API（images.py）

#### 2.1 获取图片列表
- **URL**：`/api/images`
- **方法**：`GET`
- **请求头**：`Authorization: Bearer <token>`
- **响应**：`[{"id": 1, "filename": "string", "filepath": "string", "created_at": "string"}]`

#### 2.2 上传图片
- **URL**：`/api/images/upload`
- **方法**：`POST`
- **请求头**：`Authorization: Bearer <token>`
- **请求体**：`multipart/form-data`，包含 `file` 字段
- **响应**：`{"message": "图片上传成功"}`

#### 2.3 删除图片
- **URL**：`/api/images/delete/<id>`
- **方法**：`DELETE`
- **请求头**：`Authorization: Bearer <token>`
- **响应**：`{"message": "图片删除成功"}`

## 四、JWT 认证

### 1. Token 生成

在登录成功后，使用 PyJWT 生成 JWT token：

```python
token = jwt.encode({
    'user_id': user.id,
    'exp': datetime.utcnow() + timedelta(seconds=Config.JWT_EXPIRATION_DELTA)
}, Config.JWT_SECRET_KEY, algorithm='HS256')
```

### 2. Token 验证

在需要认证的 API 中，使用辅助函数验证 token：

```python
def verify_token(token):
    try:
        payload = jwt.decode(token, Config.JWT_SECRET_KEY, algorithms=['HS256'])
        return payload['user_id']
    except jwt.ExpiredSignatureError:
        return None
    except jwt.InvalidTokenError:
        return None
```

## 五、文件上传

### 1. 上传配置

在 `config.py` 中配置文件上传相关参数：

```python
UPLOAD_FOLDER = os.path.join(os.getcwd(), 'uploads')  # 上传目录
ALLOWED_EXTENSIONS = {'png', 'jpg', 'jpeg', 'gif'}  # 允许的文件类型
MAX_CONTENT_LENGTH = 16 * 1024 * 1024  # 最大文件大小（16MB）
```

### 2. 上传逻辑

```python
@images_bp.route('/upload', methods=['POST'])
def upload_image():
    # 验证 token
    # ...
    
    # 检查文件
    if 'file' not in request.files:
        return jsonify({'message': '请选择文件'}), 400
    
    file = request.files['file']
    
    if file.filename == '':
        return jsonify({'message': '请选择文件'}), 400
    
    # 检查文件类型
    if file and allowed_file(file.filename):
        # 安全处理文件名
        filename = secure_filename(file.filename)
        
        # 保存文件
        filepath = os.path.join(Config.UPLOAD_FOLDER, filename)
        file.save(filepath)
        
        # 记录到数据库
        # ...
        
        return jsonify({'message': '图片上传成功'}), 201
    else:
        return jsonify({'message': '不允许的文件类型'}), 400
```

## 六、数据库操作

### 1. 查询操作

```python
# 查询所有图片
images = Image.query.all()

# 查询特定用户的图片
images = Image.query.filter_by(user_id=user_id).all()

# 按创建时间排序
images = Image.query.order_by(Image.created_at.desc()).all()
```

### 2. 插入操作

```python
# 创建新用户
new_user = User(
    username=data['username'],
    password=hashed_password
)

# 添加到数据库
 db.session.add(new_user)
 db.session.commit()
```

### 3. 删除操作

```python
# 查询要删除的图片
image = Image.query.filter_by(id=image_id, user_id=user_id).first()

# 删除图片
 db.session.delete(image)
 db.session.commit()
```

## 七、错误处理

在 API 中使用 try-except 块处理异常：

```python
@auth_bp.route('/login', methods=['POST'])
def login():
    try:
        # 登录逻辑
        # ...
        return jsonify({'token': token, 'message': '登录成功'}), 200
    except Exception as e:
        return jsonify({'message': f'登录失败: {str(e)}'}), 500
```

## 八、开发与部署

### 1. 安装依赖

```bash
pip install -r requirements.txt
```

### 2. 启动开发服务器

```bash
python run.py
```

### 3. 环境变量配置

在 `.env` 文件中配置环境变量：

```
# 应用配置
SECRET_KEY=your-secret-key

# 数据库配置
DB_HOST=localhost
DB_PORT=3306
DB_USER=root
DB_PASSWORD=password
DB_NAME=vue_flask_db

# JWT配置
JWT_SECRET_KEY=your-jwt-secret-key
```

### 4. 生产部署建议

- 使用 Gunicorn 或 uWSGI 部署 Flask 应用
- 配置 Nginx 作为反向代理
- 使用 HTTPS 加密传输
- 配置日志记录
- 使用进程管理器（如 Supervisor）管理应用进程

## 九、注意事项

1. **安全性**：
   - 使用 bcrypt 对密码进行哈希存储
   - 确保 JWT 密钥的安全性
   - 验证用户权限，确保用户只能访问自己的资源

2. **性能**：
   - 关闭 SQLAlchemy 的修改跟踪
   - 合理使用索引优化数据库查询
   - 限制上传文件大小

3. **可靠性**：
   - 实现适当的错误处理
   - 配置日志记录
   - 定期备份数据库

4. **可扩展性**：
   - 使用蓝图组织代码，便于扩展新功能
   - 设计合理的数据模型，便于后续扩展

## 十、扩展建议

1. 添加用户角色管理
2. 实现图片分类功能
3. 添加图片压缩和处理功能
4. 实现图片分享和权限管理
5. 添加日志记录和监控
6. 实现 API 文档自动生成（如使用 Swagger）
7. 添加单元测试和集成测试
8. 实现缓存机制，提高性能
9. 添加邮件通知功能
10. 实现多语言支持