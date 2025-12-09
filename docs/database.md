# Vue3 + Flask 登录与图片展示系统数据库设计

## 一、数据库概述

本系统使用 MySQL 数据库存储用户和图片信息。数据库设计遵循关系型数据库的设计原则，确保数据的完整性、一致性和安全性。

## 二、数据库表结构

### 1. 用户表（users）

**功能**：存储系统用户信息，包括用户名和密码哈希。

**表结构**：

| 字段名     | 数据类型    | 长度  | 约束条件                | 描述                  |
|-----------|-------------|------|------------------------|----------------------|
| id        | INT         |      | PRIMARY KEY, AUTO_INCREMENT | 用户ID，主键，自增  |
| username  | VARCHAR     | 50   | UNIQUE, NOT NULL       | 用户名，唯一，不能为空  |
| password  | VARCHAR     | 100  | NOT NULL               | 密码哈希，不能为空     |
| created_at| DATETIME    |      | DEFAULT CURRENT_TIMESTAMP | 创建时间，默认当前时间 |

**创建语句**：

```sql
CREATE TABLE `users` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `username` varchar(50) NOT NULL,
  `password` varchar(100) NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `username` (`username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

### 2. 图片表（images）

**功能**：存储用户上传的图片信息，包括文件名、路径和所属用户。

**表结构**：

| 字段名     | 数据类型    | 长度  | 约束条件                | 描述                  |
|-----------|-------------|------|------------------------|----------------------|
| id        | INT         |      | PRIMARY KEY, AUTO_INCREMENT | 图片ID，主键，自增  |
| user_id   | INT         |      | NOT NULL, FOREIGN KEY  | 所属用户ID，外键       |
| filename  | VARCHAR     | 255  | NOT NULL               | 文件名，不能为空        |
| filepath  | VARCHAR     | 255  | NOT NULL               | 文件路径，不能为空      |
| created_at| DATETIME    |      | DEFAULT CURRENT_TIMESTAMP | 创建时间，默认当前时间 |

**外键关系**：
- `user_id` 引用 `users` 表的 `id` 字段
- 删除用户时，级联删除该用户的所有图片

**创建语句**：

```sql
CREATE TABLE `images` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `user_id` int(11) NOT NULL,
  `filename` varchar(255) NOT NULL,
  `filepath` varchar(255) NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `user_id` (`user_id`),
  CONSTRAINT `images_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

## 三、数据库关系图

```
+-----------------+      +-----------------+
|     users       |      |     images      |
+-----------------+      +-----------------+
| id (PK)         |<-----| user_id (FK)    |
| username        |      | id (PK)         |
| password        |      | filename        |
| created_at      |      | filepath        |
+-----------------+      | created_at      |
                         +-----------------+
```

## 四、数据库索引设计

### 1. 用户表索引
- **主键索引**：`id` 字段，用于快速查找用户
- **唯一索引**：`username` 字段，确保用户名唯一，并加速用户名查询

### 2. 图片表索引
- **主键索引**：`id` 字段，用于快速查找图片
- **外键索引**：`user_id` 字段，加速根据用户查询图片的操作
- **创建时间索引**：`created_at` 字段，加速根据创建时间排序和查询的操作

## 五、数据类型选择

### 1. 整数类型
- 使用 `INT` 类型存储 ID 字段，足够满足系统需求

### 2. 字符串类型
- 使用 `VARCHAR` 类型存储可变长度的字符串，如用户名、文件名和文件路径
- 用户名长度限制为 50 个字符，足够满足大部分用户名需求
- 密码哈希使用 `VARCHAR(100)`，支持主流哈希算法的输出长度
- 文件名和文件路径使用 `VARCHAR(255)`，足够存储大多数文件路径

### 3. 日期时间类型
- 使用 `DATETIME` 类型存储创建时间，精确到秒，满足系统需求

## 六、数据完整性约束

### 1. 主键约束
- 每个表都有一个主键，确保数据的唯一性和可识别性

### 2. 唯一性约束
- 用户表的 `username` 字段添加唯一约束，确保用户名不重复

### 3. 非空约束
- 关键字段添加非空约束，确保数据的完整性

### 4. 外键约束
- 图片表的 `user_id` 字段添加外键约束，关联用户表的 `id` 字段
- 设置 `ON DELETE CASCADE`，确保删除用户时，自动删除该用户的所有图片，维护数据一致性

## 七、数据库优化建议

### 1. 查询优化
- 为频繁查询的字段添加索引，如图片表的 `user_id` 和 `created_at` 字段
- 避免在查询中使用 `SELECT *`，只选择需要的字段
- 使用 `LIMIT` 限制查询结果数量，减少数据传输

### 2. 存储优化
- 定期清理不再使用的数据，如用户删除的图片
- 考虑使用分区表，如按创建时间分区，提高查询性能
- 对于大文件，可以考虑使用文件存储服务（如阿里云 OSS、AWS S3 等），数据库只存储文件路径

### 3. 安全优化
- 使用参数化查询，防止 SQL 注入攻击
- 定期备份数据库，确保数据安全
- 限制数据库用户权限，遵循最小权限原则
- 对敏感数据（如密码）进行哈希处理，不存储明文密码

## 八、数据库连接配置

在 Flask 应用中，使用 SQLAlchemy 连接 MySQL 数据库，配置如下：

```python
SQLALCHEMY_DATABASE_URI = f'mysql+pymysql://{DB_USER}:{DB_PASSWORD}@{DB_HOST}:{DB_PORT}/{DB_NAME}?charset=utf8mb4'
SQLALCHEMY_TRACK_MODIFICATIONS = False
```

## 九、数据库操作示例

### 1. 用户注册

```python
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
```

### 2. 用户登录

```python
# 查询用户
user = User.query.filter_by(username=data['username']).first()

# 检查密码
if user and check_password_hash(user.password, data['password']):
    # 登录成功
    pass
```

### 3. 上传图片

```python
# 创建图片记录
new_image = Image(
    user_id=user_id,
    filename=filename,
    filepath=relative_path
)

# 添加到数据库
db.session.add(new_image)
db.session.commit()
```

### 4. 获取图片列表

```python
# 查询用户的所有图片
images = Image.query.filter_by(user_id=user_id).order_by(Image.created_at.desc()).all()
```

### 5. 删除图片

```python
# 查询要删除的图片
image = Image.query.filter_by(id=image_id, user_id=user_id).first()

# 删除图片
db.session.delete(image)
db.session.commit()
```

## 十、数据库迁移

在开发过程中，数据库结构可能会发生变化。建议使用数据库迁移工具（如 Flask-Migrate）来管理数据库迁移，确保数据库结构的一致性和完整性。

### 使用 Flask-Migrate 的基本步骤

1. 安装 Flask-Migrate：

```bash
pip install Flask-Migrate
```

2. 初始化迁移：

```bash
export FLASK_APP=run.py
flask db init
```

3. 创建迁移脚本：

```bash
flask db migrate -m "Initial migration"
```

4. 应用迁移：

```bash
flask db upgrade
```

## 十一、备份与恢复

### 1. 数据库备份

使用 `mysqldump` 命令备份数据库：

```bash
mysqldump -u root -p vue_flask_db > vue_flask_db_backup.sql
```

### 2. 数据库恢复

使用 `mysql` 命令恢复数据库：

```bash
mysql -u root -p vue_flask_db < vue_flask_db_backup.sql
```

## 十二、安全考虑

1. **密码安全**：使用 bcrypt 或其他安全的哈希算法对密码进行哈希处理，不存储明文密码
2. **SQL 注入防护**：使用 ORM 或参数化查询，避免 SQL 注入攻击
3. **权限控制**：限制数据库用户的权限，只授予必要的权限
4. **数据加密**：对于敏感数据，可以考虑使用加密存储
5. **定期备份**：定期备份数据库，确保数据安全
6. **访问控制**：限制数据库的网络访问，只允许应用服务器访问

## 十三、性能监控与调优

1. **慢查询日志**：开启 MySQL 的慢查询日志，分析和优化慢查询
2. **查询优化**：使用 `EXPLAIN` 命令分析查询计划，优化查询语句
3. **索引优化**：根据查询需求，合理添加索引
4. **连接池**：使用连接池管理数据库连接，提高性能
5. **缓存机制**：对于频繁访问的数据，可以考虑使用缓存（如 Redis），减少数据库访问

## 十四、扩展建议

1. **分库分表**：当数据量增大时，可以考虑分库分表，提高系统性能
2. **读写分离**：实现读写分离，提高系统的并发处理能力
3. **NoSQL 存储**：对于非结构化数据，可以考虑使用 NoSQL 数据库（如 MongoDB）存储
4. **数据归档**：对于历史数据，可以考虑归档，减少活跃数据量，提高查询性能
5. **数据压缩**：对于大表，可以考虑使用数据压缩，减少存储空间和 I/O 操作