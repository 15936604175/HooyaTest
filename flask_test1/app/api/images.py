from flask import Blueprint, request, jsonify, current_app, send_from_directory
from werkzeug.utils import secure_filename
import jwt
from ..models import db, Image
from ..config import Config
import os

# 创建蓝图实例
images_bp = Blueprint('images', __name__)

# 辅助函数：验证JWT token
def verify_token(token):
    """验证JWT token"""
    try:
        # 解码token
        payload = jwt.decode(token, Config.JWT_SECRET_KEY, algorithms=['HS256'])
        return payload['user_id']
    except jwt.ExpiredSignatureError:
        return None
    except jwt.InvalidTokenError:
        return None

# 辅助函数：检查文件扩展名是否允许
def allowed_file(filename):
    """检查文件扩展名是否允许"""
    return '.' in filename and \
           filename.rsplit('.', 1)[1].lower() in Config.ALLOWED_EXTENSIONS

@images_bp.route('/', methods=['GET'])
def get_images():
    """获取图片列表接口"""
    try:
        # 获取Authorization头
        auth_header = request.headers.get('Authorization')
        if not auth_header or not auth_header.startswith('Bearer '):
            return jsonify({'message': '无效的token'}), 401
        
        # 提取并验证token
        token = auth_header.split(' ')[1]
        user_id = verify_token(token)
        if not user_id:
            return jsonify({'message': 'token已过期或无效'}), 401
        
        # 查询该用户的所有图片
        images = Image.query.filter_by(user_id=user_id).order_by(Image.created_at.desc()).all()
        
        # 构建响应数据
        image_list = []
        for image in images:
            image_list.append({
                'id': image.id,
                'filename': image.filename,
                'filepath': image.filepath,
                'created_at': image.created_at.strftime('%Y-%m-%d %H:%M:%S')
            })
        
        return jsonify(image_list), 200
    except Exception as e:
        return jsonify({'message': f'获取图片列表失败: {str(e)}'}), 500

@images_bp.route('/upload', methods=['POST'])
def upload_image():
    """上传图片接口"""
    try:
        # 获取Authorization头
        auth_header = request.headers.get('Authorization')
        if not auth_header or not auth_header.startswith('Bearer '):
            return jsonify({'message': '无效的token'}), 401
        
        # 提取并验证token
        token = auth_header.split(' ')[1]
        user_id = verify_token(token)
        if not user_id:
            return jsonify({'message': 'token已过期或无效'}), 401
        
        # 检查是否有文件上传
        if 'file' not in request.files:
            return jsonify({'message': '请选择文件'}), 400
        
        file = request.files['file']
        
        # 检查文件名是否为空
        if file.filename == '':
            return jsonify({'message': '请选择文件'}), 400
        
        # 检查文件类型是否允许
        if file and allowed_file(file.filename):
            # 安全处理文件名
            filename = secure_filename(file.filename)
            
            # 构建文件路径
            filepath = os.path.join(Config.UPLOAD_FOLDER, filename)
            
            # 保存文件
            file.save(filepath)
            
            # 构建相对路径，用于前端访问
            relative_path = f'/uploads/{filename}'
            
            # 创建图片记录
            new_image = Image(
                user_id=user_id,
                filename=filename,
                filepath=relative_path
            )
            
            # 添加到数据库
            db.session.add(new_image)
            db.session.commit()
            
            return jsonify({'message': '图片上传成功'}), 201
        else:
            return jsonify({'message': '不允许的文件类型'}), 400
    except Exception as e:
        return jsonify({'message': f'上传图片失败: {str(e)}'}), 500

@images_bp.route('/delete/<int:image_id>', methods=['DELETE'])
def delete_image(image_id):
    """删除图片接口"""
    try:
        # 获取Authorization头
        auth_header = request.headers.get('Authorization')
        if not auth_header or not auth_header.startswith('Bearer '):
            return jsonify({'message': '无效的token'}), 401
        
        # 提取并验证token
        token = auth_header.split(' ')[1]
        user_id = verify_token(token)
        if not user_id:
            return jsonify({'message': 'token已过期或无效'}), 401
        
        # 查询图片
        image = Image.query.filter_by(id=image_id, user_id=user_id).first()
        if not image:
            return jsonify({'message': '图片不存在或无权访问'}), 404
        
        # 删除物理文件
        file_path = os.path.join(Config.UPLOAD_FOLDER, image.filename)
        if os.path.exists(file_path):
            os.remove(file_path)
        
        # 从数据库删除记录
        db.session.delete(image)
        db.session.commit()
        
        return jsonify({'message': '图片删除成功'}), 200
    except Exception as e:
        return jsonify({'message': f'删除图片失败: {str(e)}'}), 500

@images_bp.route('/uploads/<filename>', methods=['GET'])
def get_uploaded_file(filename):
    """提供上传文件的访问接口"""
    try:
        # 发送文件
        return send_from_directory(Config.UPLOAD_FOLDER, filename)
    except Exception as e:
        return jsonify({'message': f'访问文件失败: {str(e)}'}), 500