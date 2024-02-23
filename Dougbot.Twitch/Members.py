from datetime import timezone

from discord import Member


async def get_member(member: Member, database):
    collection = database.Members
    db_member = await collection.find_one({'_id': str(member.id)})
    if db_member is None and hasattr(member, 'name'):
        # Insert new member
        db_member = {
            '_id': str(member.id),
            'name': member.name,
            'global_name': member.display_name,
            'nick': member.nick,
            'roles': [str(role.id) for role in member.roles],
            'joined_at': member.joined_at.astimezone(timezone.utc),
            'created_at': member.created_at.astimezone(timezone.utc),
            'edits': []
        }
        await collection.insert_one(db_member)
    return db_member


async def update_member(db_member, database):
    collection = database.Members
    await collection.update_one({'_id': db_member['_id']}, {'$set': db_member})


async def get_member_by_mc_redeem(code: str, database):
    collection = database.Members
    db_member = await collection.find_one({'mc_redeem': code})
    return db_member


async def get_all_members(database):
    collection = database.Members
    cursor = collection.find()
    members = await cursor.to_list(length=None)
    return members
