from flask import Blueprint, request, jsonify
from werkzeug.security import generate_password_hash, check_password_hash
import jwt
from datetime import datetime, timedelta
from ..models import db, User
from ..config import Config

# 创建蓝图实例
auth_bp = Blueprint('auth', __name__)

@auth_bp.route('/register', methods=['POST'])
def register():
    """用户注册接口"""
    try:
        # 获取请求数据
        data = request.get_json()
        
        # 检查必填字段
        if not data or 'username' not in data or 'password' not in data:
            return jsonify({'message': '请提供用户名和密码'}), 400
        
        # 检查用户名是否已存在
        existing_user = User.query.filter_by(username=data['username']).first()
        if existing_user:
            return jsonify({'message': '用户名已存在'}), 400
        
        # 生成密码哈希
        hashed_password = generate_password_hash(data['password'], method='sha256')
        
        # 创建新用户
        new_user = User(
            username=data['username'],
            password=hashed_password
        )
        
        # 添加到数据库
        db.session.add(new_user)
        db.session.commit()
        
        return jsonify({'message': '注册成功'}), 201
    except Exception as e:
        return jsonify({'message': f'注册失败: {str(e)}'}), 500

@auth_bp.route('/login', methods=['POST'])
def login():
    """用户登录接口"""
    try:
        # 获取请求数据
        data = request.get_json()
        
        # 检查必填字段
        if not data or 'username' not in data or 'password' not in data:
            return jsonify({'message': '请提供用户名和密码'}), 400
        
        # 查找用户
        user = User.query.filter_by(username=data['username']).first()
        
        # 检查用户是否存在以及密码是否正确
        if not user or not check_password_hash(user.password, data['password']):
            return jsonify({'message': '用户名或密码错误'}), 401
        
        # 生成JWT token
        token = jwt.encode({
            'user_id': user.id,
            'exp': datetime.utcnow() + timedelta(seconds=Config.JWT_EXPIRATION_DELTA)  # 过期时间
        }, Config.JWT_SECRET_KEY, algorithm='HS256')
        
        return jsonify({'token': token, 'message': '登录成功'}), 200
    except Exception as e:
        return jsonify({'message': f'登录失败: {str(e)}'}), 500

@auth_bp.route('/logout', methods=['POST'])
def logout():
    """用户登出接口"""
    # JWT是无状态的，登出只需客户端删除token即可
    return jsonify({'message': '登出成功'}), 200

@auth_bp.route('/refresh', methods=['POST'])
def refresh():
    """刷新token接口"""
    try:
        # 获取Authorization头
        auth_header = request.headers.get('Authorization')
        if not auth_header or not auth_header.startswith('Bearer '):
            return jsonify({'message': '无效的token'}), 401
        
        # 提取旧token
        old_token = auth_header.split(' ')[1]
        
        # 验证旧token
        payload = jwt.decode(old_token, Config.JWT_SECRET_KEY, algorithms=['HS256'])
        
        # 生成新token
        new_token = jwt.encode({
            'user_id': payload['user_id'],
            'exp': datetime.utcnow() + timedelta(seconds=Config.JWT_EXPIRATION_DELTA)  # 过期时间
        }, Config.JWT_SECRET_KEY, algorithm='HS256')
        
        return jsonify({'token': new_token, 'message': 'token刷新成功'}), 200
    except jwt.ExpiredSignatureError:
        return jsonify({'message': 'token已过期'}), 401
    except jwt.InvalidTokenError:
        return jsonify({'message': '无效的token'}), 401
    except Exception as e:
        return jsonify({'message': f'刷新token失败: {str(e)}'}), 500