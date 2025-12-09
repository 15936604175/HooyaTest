from flask_sqlalchemy import SQLAlchemy
from datetime import datetime

# 创建SQLAlchemy实例
db = SQLAlchemy()

class User(db.Model):
    """用户模型"""
    __tablename__ = 'users'  # 表名
    
    id = db.Column(db.Integer, primary_key=True, autoincrement=True)  # 主键，自增
    username = db.Column(db.String(50), unique=True, nullable=False)  # 用户名，唯一，不能为空
    password = db.Column(db.String(100), nullable=False)  # 密码哈希，不能为空
    created_at = db.Column(db.DateTime, default=datetime.utcnow)  # 创建时间，默认当前时间
    
    # 关系定义，一个用户可以有多张图片
    images = db.relationship('Image', backref='user', lazy=True)  # 反向引用为user，懒加载
    
    def __repr__(self):
        return f'<User {self.username}>'

class Image(db.Model):
    """图片模型"""
    __tablename__ = 'images'  # 表名
    
    id = db.Column(db.Integer, primary_key=True, autoincrement=True)  # 主键，自增
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)  # 外键，关联users表的id
    filename = db.Column(db.String(255), nullable=False)  # 文件名，不能为空
    filepath = db.Column(db.String(255), nullable=False)  # 文件路径，不能为空
    created_at = db.Column(db.DateTime, default=datetime.utcnow)  # 创建时间，默认当前时间
    
    def __repr__(self):
        return f'<Image {self.filename}>'