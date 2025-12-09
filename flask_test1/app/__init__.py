from flask import Flask
from flask_cors import CORS
from .config import Config
from .models import db

# 创建应用实例的工厂函数
def create_app():
    # 创建Flask应用实例
    app = Flask(__name__)
    
    # 加载配置
    app.config.from_object(Config)
    
    # 初始化数据库
    db.init_app(app)
    
    # 配置CORS，允许所有跨域请求
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